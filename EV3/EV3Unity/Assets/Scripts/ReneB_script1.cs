using UnityEngine;
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
// The communication between Unity and the robot is done with EV3WifiLib.
// The robot runs a TCP socket server and Unity a socket client, meaning the robot listens
// to a specific port and Unity initiates the action by sending a request to this port.
// Sending from Unity is done synchrounously as only short messages are sent and the call
// only blocks until the data is sent, regardless whether or not the endpoint exists.
// Receiving from the robot is done asynchrounously as receiving data back from the robot
// after sending a request can take a while, depending on the server implementation on the robot.

public class ReneB_script1 : MonoBehaviour {
	private EV3WifiOrSimulation myEV3;
	private string ipAddress = "IP address";
	private Rigidbody rb;
	private String strDistance;
	private String strAngle;
	private string strEncoderB;
	private string strEncoderC;
	private float speed;
	private long ms, msPrevious = 0;
	private float moveHorizontal, moveVertical = 0f;
	private float distance = 0.0f;
	private float angle = 0.0f;
	private float encoderB = 0.0f;
	private float encoderC = 0.0f;
	private float encoderBPrevious = 0.0f;
	private float encoderCPrevious = 0.0f;
	private float anglePrevious = 0.0f;
	private bool calibrated = false;

	void Start() {
		rb = GetComponent<Rigidbody>();
		myEV3 = new EV3WifiOrSimulation ();
	}

	void Update () {
	
	}

	void FixedUpdate () {
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > 100) {
			moveHorizontal = Input.GetAxis ("Horizontal");
			moveVertical = Input.GetAxis ("Vertical");
			Input.ResetInputAxes(); // To prevent double input.
			if (moveHorizontal > 0) {
				myEV3.SendMessage ("Right", "0");
			}
			else if (moveHorizontal < 0) {
				myEV3.SendMessage ("Left", "0");
			}
			else if (moveVertical > 0) {
				myEV3.SendMessage ("Forward", "0");
			}
			else if (moveVertical < 0) {
				myEV3.SendMessage ("Backward", "0");
			}
				
			string strMessage = myEV3.ReceiveMessage ("EV3_OUTBOX0");
			
			if (strMessage != "")
			{
				string[] data = strMessage.Split(' ');
				if (data.Length == 4)
				{
					strDistance = data[0];
					strAngle = data[1];
					strEncoderB = data[2];
					strEncoderC = data[3];
				}
			}
				
			if (float.TryParse (strAngle, out angle) && float.TryParse (strEncoderB, out encoderB) && float.TryParse (strEncoderC, out encoderC)) {
				float encoderBDelta = encoderB - encoderBPrevious;
				float encoderCDelta = encoderC - encoderCPrevious;
				float angleDelta = angle - anglePrevious;
				encoderBPrevious = encoderB;
				encoderCPrevious = encoderC;
				anglePrevious = angle; 
				if (calibrated) {
					Quaternion rot = Quaternion.Euler (0, angleDelta, 0);
					rb.MoveRotation (rb.rotation * rot);
					rb.MovePosition (transform.position + ((encoderBDelta + encoderCDelta) / 200.0f) * transform.forward);
				}
				calibrated = true;
			}
			msPrevious = ms;
		}
	}

	void OnGUI()
	{
		GUIStyle styleTextField = new GUIStyle (GUI.skin.textField);
		styleTextField.fontSize = 24;
		ipAddress = GUILayout.TextField(ipAddress, styleTextField);
		GUIStyle styleButton = new GUIStyle (GUI.skin.button);
		styleButton.fontSize = 24;
		if (myEV3.isConnected == false) {
			styleButton.normal.textColor = Color.red;
			if (GUILayout.Button ("Connect", styleButton, GUILayout.Width(200), GUILayout.Height(50))) {
				myEV3.simOnly = false;
				if (myEV3.Connect ("1234", ipAddress) == true) {
					// Read the first message after a physical connection is made because the first message might be invalid.
					Debug.Log ("Connection succeeded");
					myEV3.ReceiveMessage ("EV3_OUTBOX0");
					// Set calibrated to false right after a switch from simulation to physical or vice versa to prevent jumping of position.
					calibrated = false;
				} else {
					Debug.Log ("Connection failed");
					myEV3.simOnly = true;
				}
			}
		} else {
			styleButton.normal.textColor = Color.green;
			if (GUILayout.Button ("Disconnect", styleButton, GUILayout.Width(200), GUILayout.Height(50))) {
				myEV3.Disconnect ();
				myEV3.simOnly = true;
				// Set calibrated to false right after a switch from simulation to physical or vice versa to prevent jumping of position.
				calibrated = false;

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
	private float distance = 0.0f;
	private float angle = 0.0f;
	private float encoderB = 0.0f;
	private float encoderC = 0.0f;

	public EV3WifiOrSimulation()
	{
		myEV3 = new EV3Wifi ();
	}

	public bool Connect(string serialNumber, string IPadddress)
	{
		if (simOnly) {
			return ConnectSim(serialNumber, IPadddress);
		} else {
			isConnected = myEV3.Connect(serialNumber, IPadddress);
			return isConnected;
		}
	}

	public void Disconnect()
	{
		if (simOnly) {
			DisconnectSim();
		} else {
			myEV3.Disconnect();
			isConnected = false;
		}
	}

	public void SendMessage(string msg, string mbox)
	{
		if (simOnly) {
			SendMessageSim(msg, mbox);
		} else {
			myEV3.SendMessage(msg, mbox);
		}
	}

	public string ReceiveMessage(string mbox)
	{
		if (simOnly) {
			return ReceiveMessageSim(mbox);
		} else {
			return myEV3.ReceiveMessage(mbox);
		}
	}

	private bool ConnectSim(string serialNumber, string IPadddress)
	{
		return true;
	}

	private void DisconnectSim()
	{
	}

	private void SendMessageSim(string msg, string mbox)
	{
		if (msg == "Forward") {
			encoderB = encoderB + 100.0f;
			encoderC = encoderC + 100.0f;
		} else if (msg == "Backward") {
			encoderB = encoderB - 100.0f;
			encoderC = encoderC - 100.0f;
		} else if (msg == "Left") {
			angle = angle - 30.0f;
		} else if (msg == "Right") {
			angle = angle + 30.0f;
		}
	}

	private string ReceiveMessageSim(string mbox)
	{
		return distance.ToString () + " " + angle.ToString () + " " + encoderB.ToString () + " " + encoderC.ToString ();
	}
}
