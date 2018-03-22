using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Text;
using System.Threading;
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

	public float speed;
	private Rigidbody rb;
	private EV3Wifi myEV3;
	private String strDistance = "";
	private String strAngle = "";
	private long ms, msPrevious = 0;
	private float moveHorizontal, moveVertical = 0f;

	void Start() {
		rb = GetComponent<Rigidbody>();
		myEV3 = new EV3Wifi();
		string ipAddress = "192.168.43.119";
		if (myEV3.Connect ("1234", ipAddress) == true)
		{
			Debug.Log ("Connection succeeded");
		}
		else
		{
			Debug.Log ("Connection failed");
		}
	}

	void Update () {
	
	}

	void FixedUpdate () {			
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > 100) {
			moveHorizontal = Input.GetAxis ("Horizontal");
			moveVertical = Input.GetAxis ("Vertical");
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
				if (data.Length == 2)
				{
					strDistance = data[0];
					strAngle = data[1];
				}
			}


			Debug.Log("Distance: " + strDistance);
			float distance;
			if (float.TryParse (strDistance, out distance)) {
				moveHorizontal = (distance - 50) / 1;
				moveVertical = 0;
				Vector3 position = new Vector3((distance - 50)/10, (float) 0.1, 0);
				rb.MovePosition (position);

				float speed = (float) ((distance - 50.0) * 2);
				// Limit speed to [-100, 100] interval.
				speed = Math.Max(-100, speed);
				speed = Math.Min(100, speed);
				myEV3.SendMessage("Speed " + speed.ToString(), "0");
			}
			msPrevious = ms;
		}
	}
}
