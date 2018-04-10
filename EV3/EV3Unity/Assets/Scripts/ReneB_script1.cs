using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine.AI;
using Newtonsoft.Json;
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
	private VisionClient myVision;
	private string ipAddress = "IP address";
	private Rigidbody rb;
	private Vector3 startPosition;
	private Quaternion startRotation;
	private float angleMoved = 0.0f;
	private float distanceMoved = 0.0f;
	private float distanceToObject = 0.0f;
	private float angleMovedPrevious = 0.0f;
	private float distanceMovedPrevious = 0.0f;
	private long ms, msPrevious, msPreviousTask = 0;
	private bool calibrated = false;
	private bool guiConnectEV3 = false;
	private bool guiDisconnectEV3 = false;
	private bool guiConnectVision = false;
	private bool guiDisconnectVision = false;
	private bool guiReset = true;
	private int taskReady = 1;
	private NavMeshPath path;
	private bool gotoTarget = false;
	private VisionData visionData = null;
	// To indicate robot is ready for the next task.

	void Start ()
	{
		rb = GetComponent<Rigidbody> ();
		myEV3 = new EV3WifiOrSimulation ();
		myVision = new VisionClient ();
		path = new NavMeshPath ();
		startPosition = rb.transform.position;
		startRotation = rb.rotation;
	}

	void Update ()
	{
	
	}

	void FixedUpdate ()
	{
		// We connect / disconnect with the EV3 in this thread (and not in the GUI thread) because the EV3 SendMessage and ReceiveMessage also happen in this thread.
		if (guiConnectEV3) {
			if (myEV3.Connect ("1234", ipAddress) == true) {
				Debug.Log ("EV3 connection succeeded");
				// Set calibrated to false right after a switch from simulation to physical or vice versa to prevent jumping of position.
				calibrated = false;
			} else {
				Debug.Log ("EV3 connection failed");
			}
			guiConnectEV3 = false;
		} else if (guiDisconnectEV3) {
			myEV3.Disconnect ();
			// Set calibrated to false right after a switch from simulation to physical or vice versa to prevent jumping of position.
			calibrated = false;
			// Indicate disconnection request is handled.
			guiDisconnectEV3 = false;
		}
		if (guiConnectVision) {
			myVision.Connect ();
			// Indicate connection request is handled.
			guiConnectVision = false;
		} else if (guiDisconnectVision) {
			myVision.Disconnect ();
			// Indicate disconnection request is handled.
			guiDisconnectVision = false;
		}

		if (guiReset) {
			myEV3.SendMessage ("Reset", "0");
			angleMoved = 0.0f;
			distanceMoved = 0.0f;
			angleMovedPrevious = 0.0f;
			distanceMovedPrevious = 0.0f;
			rb.transform.position = startPosition;
			rb.rotation = startRotation;
			guiReset = false;
			// Wait for robot to reset and flush the current message which contains pre-reset values.
			Thread.Sleep (500);
			myEV3.ReceiveMessage ("EV3_OUTBOX0");
		}

		if (myVision.isConnected) {
			string msg = myVision.ReceiveMessage ();
			visionData = JsonConvert.DeserializeObject<VisionData> (msg);
		} else {
			visionData = null;
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

			var targetObject = GameObject.Find ("Ball");
			if (leftMouseButtonClicked) {
				targetObject.transform.position = mPosition;
			}

			Input.ResetInputAxes (); // To prevent double input.
			if (moveVertical > 0) {			// Forward
				myEV3.SendMessage ("Move 0 10 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
			} else if (moveVertical < 0) {	// Backward
				myEV3.SendMessage ("Move 0 -10 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
			} else if (moveHorizontal < 0) {	// Left
				myEV3.SendMessage ("Move -15 0 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
			} else if (moveHorizontal > 0) {	// Right
				myEV3.SendMessage ("Move 15 0 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
			} else if (fPressed) {
				myEV3.SendMessage ("Move 0 5000 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
			} else if (bPressed) {
				myEV3.SendMessage ("Move 0 -5000 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
			} else if (tPressed) {
				// Execute task: Move to target object.
				gotoTarget = true;
			}

			if (gotoTarget) {
				if (taskReady == 1) {
					bool result = NavMesh.CalculatePath (transform.position, targetObject.transform.position, NavMesh.AllAreas, path);
					if (result) {
						float targetDistance = Vector3.Distance (rb.transform.position, path.corners [1]);
						Quaternion rotPlus90 = Quaternion.Euler (0, 90, 0);
						Quaternion rotMin90 = Quaternion.Euler (0, -90, 0);
						var relativePos = path.corners [1] - rb.transform.position;
						var relativeRot = Quaternion.LookRotation (relativePos);
						float targetAngle = Quaternion.Angle (transform.rotation, relativeRot);
						float targetAnglePlus90 = Quaternion.Angle (transform.rotation * rotPlus90, relativeRot);
						float targetAngleMin90 = Quaternion.Angle (transform.rotation * rotMin90, relativeRot);
						targetAngle = targetAnglePlus90 < targetAngleMin90 ? targetAngle : -targetAngle;
						myEV3.SendMessage ("Move " + targetAngle.ToString () + " " + targetDistance.ToString () + " 30", "0"); // Move angle (-180 .. 180) distance (cm) power (0..100)
						msPreviousTask = ms;
					}
				}
				if (Vector3.Distance (rb.transform.position, targetObject.transform.position) < 5) {
					gotoTarget = false;
				}
			}

			string strMessage = myEV3.ReceiveMessage ("EV3_OUTBOX0");

			// Check if the message is valid. The first message received after connecting with the EV3 is "Accept:EV340"
			// indicating that the connection has been established. This is not a valid message.
			if (strMessage != "") {
				string[] data = strMessage.Split (' ');
				if (data.Length == 4) {
					if (int.TryParse (data [0], out taskReady) && float.TryParse (data [1], out angleMoved) && float.TryParse (data [2], out distanceMoved) && float.TryParse (data [3], out distanceToObject)) {
						// If a new task just has been sent to the robot, the taskReady will still be on '1'.
						// It takes about 3 time ticks of 100 ms to receive taskReady = 0 back from the robot.
						// Therefore we force taskReady to 0 for 5 time ticks after the task has been sent.
						if (ms - msPreviousTask < 5 * Constants.TimeTickMs) {
							taskReady = 0;
						}
						float distanceMovedDelta = distanceMoved - distanceMovedPrevious;
						float angleMovedDelta = angleMoved - angleMovedPrevious;
						distanceMovedPrevious = distanceMoved;
						angleMovedPrevious = angleMoved;
						if (calibrated) {
							if (visionData == null) {
								Quaternion rot = Quaternion.Euler (0, angleMovedDelta, 0);
								rb.MoveRotation (rb.rotation * rot);
								rb.MovePosition (rb.transform.position + distanceMovedDelta * rb.transform.forward);
							} else {
								Vector3 vec = new Vector3 ();
								vec.x = visionData.bot1 [1] / 10.0f;
								vec.y = 3.3f;
								vec.z = visionData.bot1 [2] / 10.0f;
								rb.transform.position = vec;
								rb.rotation = Quaternion.Euler (0, visionData.bot1 [0], 0);
							}
						}
						calibrated = true;

						// Draw a wall in front of the robot when an object is detected.
						var wallObject = GameObject.Find ("Wall");
						MeshRenderer renderWall = wallObject.GetComponentInChildren<MeshRenderer> ();
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
		styleTextField.fontSize = 12;
		ipAddress = GUILayout.TextField (ipAddress, styleTextField);
		GUIStyle styleButton = new GUIStyle (GUI.skin.button);
		styleButton.fontSize = 12;
		if (myEV3.isConnected == false) {
			styleButton.normal.textColor = Color.red;
			if (GUILayout.Button ("Connect EV3", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiConnectEV3 = true;
			}
		} else if (myEV3.isConnected == true) {
			styleButton.normal.textColor = Color.green;
			if (GUILayout.Button ("Disconnect EV3", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiDisconnectEV3 = true;
			}
		}
		if (myVision.isConnected == false) {
			styleButton.normal.textColor = Color.red;
			if (GUILayout.Button ("Connect vision", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiConnectVision = true;
			}
		} else if (myVision.isConnected == true) {
			styleButton.normal.textColor = Color.green;
			if (GUILayout.Button ("Disconnect vision", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiDisconnectVision = true;
			}
		}
		styleButton.normal.textColor = Color.red;
		if (GUILayout.Button ("Reset", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
			guiReset = true;
		}
		GUIStyle styleLabel = new GUIStyle (GUI.skin.label);
		styleLabel.fontSize = 12;
		if (myEV3.simOnly) {
			GUILayout.Label ("Simulation mode", styleLabel);
		} else {
			GUILayout.Label ("Physical mode", styleLabel);
		}
	}

	void OnApplicationQuit ()
	{
		if (guiDisconnectEV3) {
			myEV3.Disconnect ();
		}
		if (guiDisconnectVision) {
			myVision.Disconnect ();
		}
	}
}

public class VisionData
{
	public float[] bot1 = { 0.0f, 0.0f, 0.0f };
	public float[] bot2 = { 0.0f, 0.0f, 0.0f };

}

public class EV3WifiOrSimulation
{
	public bool simOnly = true;
	public bool isConnected = false;
	private EV3Wifi myEV3;
	private bool newMessageArrived = false;
	private string lastMessageSent = "";
	private float angleMoved = 0.0f;
	private float distanceMoved = 0.0f;
	private float distanceToObject = 0.0f;
	private float angleToMoveRemaining = 0.0f;
	private float distanceToMoveRemaining = 0.0f;
	float angleToMovePerTimeTick, distanceToMovePerTimeTick;
	float angleToMove, distanceToMove, pwr;
	int taskReady = 1;
	// To indicate robot is ready for the next task

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
	// This message contains a distanceToMove, angleToMove and pwr.
	// The simulated robot first turns by angleToMove degrees and then moves straight for distanceToMove units with power pwr.
	private string ReceiveMessageSim (string mbox)
	{
		if (newMessageArrived) {
			string[] messageStrings = lastMessageSent.Split (' ');
			if (messageStrings [0] == "Move" && float.TryParse (messageStrings [1], out angleToMove) && float.TryParse (messageStrings [2], out distanceToMove) && float.TryParse (messageStrings [3], out pwr)) {
				angleToMovePerTimeTick = pwr * (angleToMove > 0 ? Constants.PowerToAnglePerTimeTick : -Constants.PowerToAnglePerTimeTick);
				distanceToMovePerTimeTick = pwr * (distanceToMove > 0 ? Constants.PowerToDistancePerTimeTick : -Constants.PowerToDistancePerTimeTick);
				angleToMoveRemaining = angleToMove != 0 ? Math.Abs (angleToMove) - Math.Abs (angleToMovePerTimeTick) : 0;
				distanceToMoveRemaining = distanceToMove != 0 ? Math.Abs (distanceToMove) - Math.Abs (distanceToMovePerTimeTick) : 0;
				newMessageArrived = false;
				taskReady = 0; // Indicate robot is not ready for the next task.
			} else if (messageStrings [0] == "Reset") {
				angleMoved = 0.0f;
				distanceMoved = 0.0f;
			}
		} else if (angleToMove != 0 || distanceToMove != 0) {
			if (angleToMoveRemaining > 0) {
				angleToMoveRemaining = angleToMoveRemaining - Math.Abs (angleToMovePerTimeTick);
			} else if (distanceToMoveRemaining > 0) {
				distanceToMoveRemaining = distanceToMoveRemaining - Math.Abs (distanceToMovePerTimeTick);
			}
		}
		if (angleToMove != 0) {				// First handle the turn.
			angleMoved = angleMoved + angleToMovePerTimeTick;
			if (angleToMoveRemaining <= 0) {
				// Last time tick for this turn, make sure to turn not more than angleToMove.
				angleMoved = angleMoved + (angleToMovePerTimeTick > 0 ? angleToMoveRemaining : -angleToMoveRemaining);
				angleToMove = 0; // Indicate turning is done.
			}
		} else if (distanceToMove != 0) {	// Then handle the move.
			distanceMoved = distanceMoved + distanceToMovePerTimeTick;
			if (distanceToMoveRemaining <= 0) {
				// Last time tick for this move, make sure to move not more than distanceToMove.
				distanceMoved = distanceMoved + (distanceToMovePerTimeTick > 0 ? distanceToMoveRemaining : -distanceToMoveRemaining);
				distanceToMove = 0; // Indicate moving is done.
			}
			
		} else {
			taskReady = 1; // Indicate robot is ready for the next task.
		}
		return taskReady.ToString () + " " + angleMoved.ToString () + " " + distanceMoved.ToString () + " " + distanceToObject.ToString ();
	}
}
