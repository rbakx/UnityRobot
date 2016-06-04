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
	private long ms, msPrevious = 0;
	private float moveHorizontal, moveVertical = 0f;

	void Start() {
		rb = GetComponent<Rigidbody>();
		myEV3 = new EV3Wifi();
		String status = myEV3.Connect();
		Debug.Log ("Connection status: " + status);
	}

	void Update () {
	
	}

	void FixedUpdate () {			
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > 100) {
			moveHorizontal = Input.GetAxis ("Horizontal");
			moveVertical = Input.GetAxis ("Vertical");
			if (moveHorizontal > 0) {
				myEV3.SendMessage("forward", "MOVE");
			}
			else if (moveHorizontal < 0) {
				myEV3.SendMessage("backward", "MOVE");
			}

			myEV3.SendMessage("get_distance", "STATUS");
			// Calling ReceiveMessage is non -blocking. It will retrieve the previous message and initiate a new message retrieval.
			strDistance = myEV3.ReceiveMessage("EV3Wifi", "DISTANCE");
			// do what you'd like with `message` here:
			Debug.Log("Distance: " + strDistance + "hi");
			float distance;
			if (float.TryParse (strDistance, out distance)) {
				moveHorizontal = (distance - 50) / 1;
				moveVertical = 0;
				Vector3 position = new Vector3((distance - 50)/10, (float) 0.1, 0);
				rb.MovePosition (position);

				if (distance > 53.0) {
					myEV3.SendMessage ("forward", "MOVE");
				} else if (distance < 47.0) {
					myEV3.SendMessage ("backward", "MOVE");
				} else {
					myEV3.SendMessage ("stop", "MOVE");
				}
			}
			msPrevious = ms;
		}
	}
}
