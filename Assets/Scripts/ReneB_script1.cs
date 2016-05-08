using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;


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
	private UdpClient socket;
	private IPEndPoint target;
	private String strDistance = "";
	private long ms, msPrevious = 0;
	private float moveHorizontal, moveVertical = 0f;
	// static array to keep the message which is filled by the asynchronous callback method.
	private static byte[] message = new byte[1024];


	static void OnUdpData(IAsyncResult result) {
		// This is what had been passed into BeginReceive as the second parameter.
		UdpClient socket = result.AsyncState as UdpClient;
		// points towards whoever had sent the message.
		IPEndPoint source = new IPEndPoint(0, 0);
		// Get the actual message and fill out the source.
		message = socket.EndReceive(result, ref source);
		// Schedule the next receive operation once reading is done.
		socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
	}

	void Start() {
		rb = GetComponent<Rigidbody>();
		// Creates a UdpClient for reading incoming data.
		// With no port number specified the UdpClient will automatically pick an available port number as the source port.
		socket = new UdpClient();
		// Schedule the first receive operation.
		socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
		target = new IPEndPoint(IPAddress.Parse("192.168.1.42"), 12345);
	}

	void Update () {
	
	}

	void FixedUpdate () {			
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > 400) {
			byte[] msg;

			moveHorizontal = Input.GetAxis ("Horizontal");
			moveVertical = Input.GetAxis ("Vertical");
			if (moveHorizontal > 0) {
				msg = Encoding.ASCII.GetBytes ("forward 150");
				socket.Send (msg, msg.Length, target);
			} else if (moveHorizontal < 0) {
				msg = Encoding.ASCII.GetBytes ("backward 150");
				socket.Send (msg, msg.Length, target);
			}

			msg = Encoding.ASCII.GetBytes("get_distance");
			msPrevious = ms;
			socket.Send(msg, msg.Length, target);
			String strDistance = Encoding.ASCII.GetString(message, 0, message.Length );
			// do what you'd like with `message` here:
			Debug.Log("Distance: " + strDistance);
			float distance;
			if (float.TryParse (strDistance, out distance)) {
				moveHorizontal = (distance - 50) / 1;
				moveVertical = 0;
				Debug.Log (distance);
				Vector3 movement = new Vector3 (moveHorizontal, 0f, moveVertical);
				//rb.AddForce (movement * speed);
				Vector3 position = new Vector3((distance - 50)/10, (float) 0.1, 0);
				rb.MovePosition (position);

				if (distance > 53.0) {
					msg = Encoding.ASCII.GetBytes ("forward 130");
					socket.Send (msg, msg.Length, target);
				} else if (distance < 47.0) {
					msg = Encoding.ASCII.GetBytes ("backward 130");
					socket.Send (msg, msg.Length, target);
				}
			}
		}
	}
}
