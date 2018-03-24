using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using EV3WifiLib;


// Example script which serves as a proof of concept of a robot controlled from Unity.
// The robot has an ultrasonic sensor which provides distance data.
// Unity reads this data and calculates the desired robot behavior.
// This behaviour is then translated into move commands sent back to te robot.
// In this simple example, the desired behavior is that the robot keeps a constant
// distance of 50 cm to a wall. In Unity the robot is represented by a car.
// This cars keeps a contant distance to the wall and it can also be controlled
// in the x direction using the 'A' and 'D' keys.
// The communication between Unity and the robot is done though UDP sockets.
// Th robot runs a socket server and Unity a socket client, meaning the robot listens
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
	private string strEncoder = "";
	private float speed;
	private long ms, msPrevious = 0;
	private float moveHorizontal, moveVertical = 0f;

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
		if (ms - msPrevious > 100) {;
			moveHorizontal = Input.GetAxis ("Horizontal");
			moveVertical = Input.GetAxis ("Vertical");
			Input.ResetInputAxes(); // To prevent double input.
			if (moveHorizontal > 0) {
				myEV3.SendMessage("Forward", "0");
			}
			else if (moveHorizontal < 0) {
				myEV3.SendMessage ("Backward", "0");
			}

			string strMessage = myEV3.ReceiveMessage("EV3_OUTBOX0");
			if (strMessage != "")
			{
				string[] data = strMessage.Split(' ');
				if (data.Length == 3)
				{
					strDistance = data[0];
					strAngle = data[1];
					strEncoder = data[2];
				}
			}


			//Debug.Log("Distance: " + strDistance);
			float distance;
			int encoder;
			if (int.TryParse (strEncoder, out encoder)) {
				Vector3 position = new Vector3(encoder/100, (float) 0.1, 0);
				rb.MovePosition (position);
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
