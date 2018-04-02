﻿using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using EV3WifiLib;


// Example script which serves as a proof of concept of the EV3 robot controlled from Unity, represented by a car object.
// In this simple example, the EV3 can be controlled using the WASD keys or the arrow keys.
// It sends back sensor information from the gyro and motor encoders which is used to move the car object.
// The scaling is such that one scale unit in Unity corresponds to 1 cm in the physical world.
// The communication between Unity and the robot is done with EV3WifiLib.
// The robot runs a TCP socket server and Unity a socket client, meaning the robot listens
// to a specific port and Unity initiates the action by sending a request to this port.
// Sending from Unity is done synchrounously as only short messages are sent and the call
// only blocks until the data is sent, regardless whether or not the endpoint exists.
// Receiving from the robot is done asynchrounously as receiving data back from the robot
// after sending a request can take a while, depending on the server implementation on the robot.


static class Constants
{
	public const int TimeTickMs = 100;
	// 100 ms
	public const float PowerToDistancePerTimeTick = 0.5f * TimeTickMs / 1000.0f;
	// 0.5 cm per pwr per second, e.g. pwr = 30 -> 15 cm per second.
	public const float PowerToAnglePerTimeTick = 3.0f * TimeTickMs / 1000.0f;
	// 3 degrees per pwr per second, e.g. pwr = 30 -> 90 degrees per second.
}


public class ReneB_script1 : MonoBehaviour
{
	private EV3WifiOrSimulation myEV3;
	private string ipAddress = "IP address";
	private Rigidbody rb;
	private float distanceMoved = 0.0f;
	private float angleTurned = 0.0f;
	private float distanceToObject = 0.0f;
	private float distanceMovedPrevious = 0.0f;
	private float angleTurnedPrevious = 0.0f;
	private long ms, msPrevious = 0;
	private bool calibrated = false;
	private bool guiConnect = false;
	private bool guiDisconnect = false;

	void Start ()
	{
		rb = GetComponent<Rigidbody> ();
		myEV3 = new EV3WifiOrSimulation ();
	}

	void Update ()
	{
	
	}

	void FixedUpdate ()
	{
		// We connect / disconnect with the EV3 in this thread (and not in the GUI thread) because the EV3 SendMessage and ReceiveMessage also happen in this thread.
		if (guiConnect) {
			if (myEV3.Connect ("1234", ipAddress) == true) {
				Debug.Log ("Connection succeeded");
				// Set calibrated to false right after a switch from simulation to physical or vice versa to prevent jumping of position.
				calibrated = false;
			} else {
				Debug.Log ("Connection failed");
			}
			// Indicate connection request is handled.
			guiConnect = false;
		} else if (guiDisconnect) {
			myEV3.Disconnect ();
			// Set calibrated to false right after a switch from simulation to physical or vice versa to prevent jumping of position.
			calibrated = false;
			// Indicate disconnection request is handled.
			guiDisconnect = false;
		}
			
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > Constants.TimeTickMs) {
			// Below code is executed once per TimeTickMs. This to limit communication to physical robot.ScreenToWorldPoint

			// Read input keys. Because we get here once in so many calls of FixedUpdate we have to use key events which last long enough.
			float moveHorizontal = Input.GetAxis ("Horizontal");
			float moveVertical = Input.GetAxis ("Vertical");
			bool fPressed = Input.GetKey (KeyCode.F);
			bool bPressed = Input.GetKey (KeyCode.B);
			bool tPressed = Input.GetKey (KeyCode.T);
			bool leftMouseButtonClicked = Input.GetMouseButton (1);
			// Get mouse position and convert to World coordinates.
			Vector3 mPosition = Input.mousePosition;
			mPosition.z = Camera.main.gameObject.transform.position.y;
			mPosition = Camera.main.ScreenToWorldPoint (mPosition);

			var targetObject = GameObject.Find ("Cylinder");
			if (leftMouseButtonClicked) {
				targetObject.transform.position = mPosition;
			}
								
			Input.ResetInputAxes (); // To prevent double input.
			if (moveVertical > 0) {			// Forward
				myEV3.SendMessage ("Move 10 0 30", "0");	// Move distance (cm) angle (-180 .. 180) power (0..100)
			} else if (moveVertical < 0) {	// Backward
				myEV3.SendMessage ("Move -10 0 30", "0");	// Move distance (cm) angle (-180 .. 180) power (0..100)
			} else if (moveHorizontal < 0) {	// Left
				myEV3.SendMessage ("Move 0 -15 30", "0");	// Move distance (cm) angle (-180 .. 180) power (0..100)
			} else if (moveHorizontal > 0) {	// Right
				myEV3.SendMessage ("Move 0 15 30", "0");	// Move distance (cm) angle (-180 .. 180) power (0..100)
			} else if (fPressed) {
				myEV3.SendMessage ("Move 5000 0 30", "0");	// Move distance (cm) angle (-180 .. 180) power (0..100)
			} else if (bPressed) {
				myEV3.SendMessage ("Move -5000 0 30", "0");	// Move distance (cm) angle (-180 .. 180) power (0..100)
			} else if (tPressed) {
				float targetDistance = Vector3.Distance (rb.transform.position, targetObject.transform.position);

				Quaternion rotPlus90 = Quaternion.Euler (0, 90, 0);
				Quaternion rotMin90 = Quaternion.Euler (0, -90, 0);
				var relativePos = targetObject.transform.position - rb.transform.position;
				var relativeRot = Quaternion.LookRotation (relativePos);
				float targetAngle = Quaternion.Angle (transform.rotation, relativeRot);
				float targetAnglePlus90 = Quaternion.Angle (transform.rotation * rotPlus90, relativeRot);
				float targetAngleMin90 = Quaternion.Angle (transform.rotation * rotMin90, relativeRot);
				targetAngle = targetAnglePlus90 < targetAngleMin90 ? targetAngle : -targetAngle;
				myEV3.SendMessage ("Move " + targetDistance.ToString () + " " + targetAngle.ToString () + " 30", "0"); // Move distance (cm) angle (-180 .. 180) power (0..100)
			}

			string strMessage = myEV3.ReceiveMessage ("EV3_OUTBOX0");

			// Check if the message is valid. The first message received after connecting with the EV3 is "Accept:EV340"
			// indicating that the connection has been established. This is not a valid message.
			if (strMessage != "") {
				string[] data = strMessage.Split (' ');
				if (data.Length == 3) {
					if (float.TryParse (data [0], out distanceMoved) && float.TryParse (data [1], out angleTurned) && float.TryParse (data [2], out distanceToObject)) {
						float distanceMovedDelta = distanceMoved - distanceMovedPrevious;
						float angleTurnedDelta = angleTurned - angleTurnedPrevious;
						distanceMovedPrevious = distanceMoved;
						angleTurnedPrevious = angleTurned;
						if (calibrated) {
							Quaternion rot = Quaternion.Euler (0, angleTurnedDelta, 0);
							rb.MoveRotation (rb.rotation * rot);
							rb.MovePosition (rb.transform.position + distanceMovedDelta * rb.transform.forward);
						}
						calibrated = true;

						// Draw a wall in fron of the robot when an object is detected.
						var wallObject = GameObject.Find ("Wall");
						MeshRenderer renderWall = wallObject.GetComponentInChildren<MeshRenderer>();
						wallObject.transform.position = rb.transform.position;
						wallObject.transform.rotation = rb.transform.rotation;
						// Place the wall in front of the robot at distanceToObject units.
						wallObject.transform.Translate (0, 0, distanceToObject + 10); // +10 to compensate for robot length.
						if (distanceToObject > 0 && distanceToObject < 30) {
							renderWall.enabled = true;
						} else {
							renderWall.enabled = false;
						}
					}
				}
			}
			msPrevious = ms;
		}
	}

	void OnGUI ()
	{
		GUIStyle styleTextField = new GUIStyle (GUI.skin.textField);
		styleTextField.fontSize = 24;
		ipAddress = GUILayout.TextField (ipAddress, styleTextField);
		GUIStyle styleButton = new GUIStyle (GUI.skin.button);
		styleButton.fontSize = 24;
		if (myEV3.isConnected == false) {
			styleButton.normal.textColor = Color.red;
			if (GUILayout.Button ("Connect", styleButton, GUILayout.Width (200), GUILayout.Height (50))) {
				guiConnect = true;
			}
		} else {
			styleButton.normal.textColor = Color.green;
			if (GUILayout.Button ("Disconnect", styleButton, GUILayout.Width (200), GUILayout.Height (50))) {
				guiDisconnect = true;
			}
		}
		GUIStyle styleLabel = new GUIStyle (GUI.skin.label);
		styleLabel.fontSize = 24;
		if (myEV3.simOnly) {
			GUILayout.Label ("Simulation mode", styleLabel);
		} else {
			GUILayout.Label ("Physical mode", styleLabel);
		}
	}

}


public class EV3WifiOrSimulation
{
	public bool simOnly = true;
	public bool isConnected = false;
	private EV3Wifi myEV3;
	private bool newMessageArrived = false;
	private string lastMessageSent = "";
	private float distanceMoved = 0.0f;
	private float angleTurned = 0.0f;
	private float distanceToObject = 0.0f;
	private float distanceToMoveRemaining = 0.0f;
	private float angleToTurnRemaining = 0.0f;
	float angleToTurnPerTimeTick, distanceToMovePerTimeTick;
	float distanceToMove, angleToTurn, pwr;

	public EV3WifiOrSimulation ()
	{
		myEV3 = new EV3Wifi ();
	}

	public bool Connect (string serialNumber, string IPadddress)
	{
		isConnected = myEV3.Connect (serialNumber, IPadddress);
		simOnly = !isConnected;
		return isConnected;
	}

	public void Disconnect ()
	{
		myEV3.Disconnect ();
		isConnected = false;
		simOnly = true;
	}

	public void SendMessage (string msg, string mbox)
	{
		if (simOnly) {
			SendMessageSim (msg, mbox);
		} else {
			myEV3.SendMessage (msg, mbox);
		}
	}

	public string ReceiveMessage (string mbox)
	{
		if (simOnly) {
			return ReceiveMessageSim (mbox);
		} else {
			return myEV3.ReceiveMessage (mbox);
		}
	}

	private bool ConnectSim (string serialNumber, string IPadddress)
	{
		return true;
	}

	private void DisconnectSim ()
	{
	}

	private void SendMessageSim (string msg, string mbox)
	{
		// Indicate new message has arrived.
		newMessageArrived = true;
		// Store msg in lastMessageSent so it can ve handled in ReceiveMessageSim to simulate robot movement.
		lastMessageSent = msg;
	}

	// ReceiveMessageSim simulates receiving a message from the robot.
	// It uses the message sent with SendMessageSim to determine the movement of the robot.
	// This message contains a distanceToMove, angleToTurn and pwr.
	// The simulated robot first turns by angleToTurn degrees and then moves straight for distanceToMove units with power pwr.
	private string ReceiveMessageSim (string mbox)
	{
		if (newMessageArrived) {
			string[] messageStrings = lastMessageSent.Split (' ');
			if (messageStrings [0] == "Move" && float.TryParse (messageStrings [1], out distanceToMove) && float.TryParse (messageStrings [2], out angleToTurn) && float.TryParse (messageStrings [3], out pwr)) {
				angleToTurnPerTimeTick = pwr * (angleToTurn > 0 ? Constants.PowerToAnglePerTimeTick : -Constants.PowerToAnglePerTimeTick);
				distanceToMovePerTimeTick = pwr * (distanceToMove > 0 ? Constants.PowerToDistancePerTimeTick : -Constants.PowerToDistancePerTimeTick);
				angleToTurnRemaining = angleToTurn != 0 ? Math.Abs (angleToTurn) - Math.Abs (angleToTurnPerTimeTick) : 0;
				distanceToMoveRemaining = distanceToMove != 0 ? Math.Abs (distanceToMove) - Math.Abs (distanceToMovePerTimeTick) : 0;
				newMessageArrived = false;
			}
		} else if (angleToTurn != 0 || distanceToMove != 0) {
			if (angleToTurnRemaining > 0) {
				angleToTurnRemaining = angleToTurnRemaining - Math.Abs (angleToTurnPerTimeTick);
			} else if (distanceToMoveRemaining > 0) {
				distanceToMoveRemaining = distanceToMoveRemaining - Math.Abs (distanceToMovePerTimeTick);
			}
		}
		if (angleToTurn != 0) {				// First handle the turn.
			angleTurned = angleTurned + angleToTurnPerTimeTick;
			if (angleToTurnRemaining <= 0) {
				// Last time tick for this turn, make sure to turn not more than angleToTurn.
				angleTurned = angleTurned + (angleToTurnPerTimeTick > 0 ? angleToTurnRemaining : -angleToTurnRemaining);
				angleToTurn = 0; // Indicate turning is done.
			}
		} else if (distanceToMove != 0) {	// Then handle the move.
			distanceMoved = distanceMoved + distanceToMovePerTimeTick;
			if (distanceToMoveRemaining <= 0) {
				// Last time tick for this move, make sure to move not more than distanceToMove.
				distanceMoved = distanceMoved + (distanceToMovePerTimeTick > 0 ? distanceToMoveRemaining : -distanceToMoveRemaining);
				distanceToMove = 0; // Indicate moving is done.
			}
		}
		return distanceMoved.ToString () + " " + angleTurned.ToString () + " " + distanceToObject.ToString ();
	}
}
