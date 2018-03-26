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
	private EV3Wifi myEV3;
	private string ipAddress = "IP address";
	private Rigidbody rb;
	private String strDistance = "";
	private String strAngle = "";
	private string strEncoderB = "";
	private string strEncoderC = "";
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
		myEV3 = new EV3Wifi();
	}

	void Update () {
	
	}

	void FixedUpdate () {
		if (myEV3.isConnected == false) {
			return;
		}
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > 100) {
			moveHorizontal = Input.GetAxis ("Horizontal");
			moveVertical = Input.GetAxis ("Vertical");
			Input.ResetInputAxes(); // To prevent double input.
			if (moveHorizontal > 0) {
				myEV3.SendMessage("Right", "0");
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

			string strMessage = myEV3.ReceiveMessage("EV3_OUTBOX0");
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
					//rb.MoveRotation (rot);
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
		ipAddress = GUILayout.TextField(ipAddress);
		GUIStyle style = new GUIStyle ();
		if (myEV3.isConnected == false) {
			style.normal.textColor = Color.red;
			if (GUILayout.Button ("Connect", style)) {
				if (myEV3.Connect ("1234", ipAddress) == true) {
					Debug.Log ("Connection succeeded");
				} else {
					Debug.Log ("Connection failed");
				}
			}
		} else {
			style.normal.textColor = Color.green;
			if (GUILayout.Button ("Disconnect", style)) {
				myEV3.Disconnect ();
			}
		}


	}

}
