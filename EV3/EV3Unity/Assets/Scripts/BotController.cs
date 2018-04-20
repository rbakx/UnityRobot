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


// ***** Description *****
// Example script which serves as a proof of concept of a EV3 bot controlled from Unity, represented by a bot object.
// A connection is established between Unity and the bot and also between Unity and a Python vision server running an OpenCV vision application.
// The bot sends sensor information from the gyro and motor encoders to Unity.
// In addition Unity receives vision data from the Python vision server.
// Both sensor data and vision data are used to position the bot in the Unity scene.
// The vision data comes from a camera above the scene. This scene can be projected by a beamer on the floor.
// The scaling is such that one scale unit in Unity corresponds to 1 cm in the physical world.
// The communication between Unity and the bot is done using the OIT11 EV3 TCP communication library EV3WifiLib.
// The communication between Unity and the vision server is done using a TCP socket client at the Unity side.
// This script contains a bot simulator to simulate the communication to the bot for testing and debugging purposes.
// As a demo, the bot can push a ball towards a goal and score.
//
// ***** Usage *****
// When the script starts the bot simulator is active by default.
// When the 'Connect EV3' button is pressed a connection with the EV3 is made.
// When the 'Connect vision' button is pressed, a connection with the vision server is made.
// Whith the WASD keys or the arrow keys the bot can be moved.
// When 'T' is pressed, the bot moves towards the target, in this case a ball.
// When 'G' is pressed, the bot pushes the bal towards the goal and scores.


static class BotConstants
{
	// 100 ms
	public const int TimeTickMs = 100;
	// 0.5 cm per pwr per second, e.g. pwr = 30 -> 15 cm per second.
	public const float PowerToDistancePerTimeTick = 0.5f * TimeTickMs / 1000.0f;
	// 3 degrees per pwr per second, e.g. pwr = 30 -> 90 degrees per second.
	public const float PowerToAnglePerTimeTick = 3.0f * TimeTickMs / 1000.0f;
	// When waypoint is reached within this distance it is considered reached.
	// An accuracy of 5 cm is possible but in case the bot does not seem to reach its waypoints, set it to 10.
	// The cause can happen when the camera is not calibrated properly.
	public const float WayPointAccuracy = 5.0f;
	// Recalculate path every RecalculatePathDistance cm.
	public const float RecalculatePathDistance = 20.0f;
	// Distance behind the ball to start the shot. Keep this a bit shorther than RecalculatePathDistance.
	// Otherwise during the shot the path is recalculated which can result in hitting the ball twice.
	// When BehindTheBallDistance is zero, the bot just touches the ball.
	public const float BehindTheBallDistance = 10.0f;
	// Distance for positioning infront of the ball before shooting.
	// If the bot has to drive into the ball to shoot the ShootingDistance must be negative!
	// When ShootingDistance is zero, the bot just touches the ball.
	public const float ShootingDistance = -10.0f;
	// Delay taskReady going from 0 (not ready) to 1 (ready).
	// This is needed when the taskReady message of a physical bot arrives earlier than the position update of the visionData.
	public const int TaskReadyDelay = 500;
}


static class VisionConstants
{
	// Height of camera above ground.
	public const float CameraHeight = 250.0f;
	// Margin of the camera image around the beamer projection as a percentage of the length / width.
	// This margin is present because the camera sees the complete beamer projection including a margin.
	// Cropping the camera image with this margin enables matching the beamer projection woth the cropped camera image.
	public const float CameraMargin = 0.1f;
}


public class BotController : MonoBehaviour
{
	public VisionData visionData = null;
	private EV3WifiOrSimulation myEV3;
	private VisionClient myVision;
	private VisionDataHandling myVisionDataHandling = null;
	private string ipAddress = "IP address";
	private Rigidbody rb;
	private Vector3 startPosition;
	private Quaternion startRotation;
	private float angleMoved = 0.0f;
	private float distanceMoved = 0.0f;
	private float distanceToObject = 0.0f;
	private float angleMovedPrevious = 0.0f;
	private float distanceMovedPrevious = 0.0f;
	private long ms, msPrevious, msPreviousTask, msStartTaskReadyExtension = 0;
	private bool calibrated = false;
	private bool guiConnectEV3 = false;
	private bool guiDisconnectEV3 = false;
	private bool guiConnectVision = false;
	private bool guiDisconnectVision = false;
	private bool guiReset = true;
	private int taskReady = 1;
	private int taskReadyPrevious = 1;
	private bool gotoTarget = false;
	private bool shootTheBall = false;
	private bool isBehindTheBall = false;
	private GameObject targetObject;
	private Vector3 goalPosition;
	private Vector3 behindGoalPosition;
	private float botLength;
	private float botHeight;
	private float ballRadius;
	private LineRenderer lineRenderer;
	private Vector3 ballPosition;
	private Vector3 fromBehindGoalToBall2DNorm;

	// Start is called on the frame when a script is enabled just before any of the Update methods is called the first time.
	void Start ()
	{
		rb = GetComponent<Rigidbody> ();
		myEV3 = new EV3WifiOrSimulation ();
		myVision = new VisionClient ();
		startPosition = rb.transform.position;
		startRotation = rb.rotation;
		targetObject = GameObject.Find ("Ball");
		// bBot and ball dimensions are used to position the Bot behind the ball before shooring and to determine marker height.
		botLength = rb.transform.localScale.z;
		botHeight = rb.transform.localScale.y;
		ballRadius = GameObject.Find ("Ball").transform.localScale.x / 2.0f;
		rb.transform.localScale = new Vector3 (15.0f, 12.5f, 26.0f);
		lineRenderer = GetComponent<LineRenderer> ();
		goalPosition = GameObject.Find ("GoalRight").transform.position;
		// Small correction to put target just behind the goal line
		behindGoalPosition = goalPosition + new Vector3(Math.Sign(goalPosition.x) * 10.0f,0.0f,0.0f);
	}

	// Update is called every frame, if the MonoBehaviour is enabled.
	void Update ()
	{

	}

	// This function is called every fixed framerate frame, if the MonoBehaviour is enabled.
	// FixedUpdate should be used instead of Update when dealing with Rigidbody.
	// For example when adding a force to a rigidbody, you have to apply the force every fixed frame inside FixedUpdate instead of every frame inside Update.
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
			// Wait for bot to reset and flush the current message which contains pre-reset values.
			Thread.Sleep (500);
			myEV3.ReceiveMessage ("EV3_OUTBOX0");
			gotoTarget = false;
			shootTheBall = false;
			isBehindTheBall = false;
			lineRenderer.positionCount = 0; // Erase the current path
		}


		// Receive vision data.
		// The ball position is updated in the scene.
		if (myVision.isConnected) {
			string msg = myVision.ReceiveMessage ();
			visionData = JsonConvert.DeserializeObject<VisionData> (msg);
			// Create myVisionDataHandling after visionData is valid.
			if (myVisionDataHandling == null) {
				myVisionDataHandling = new VisionDataHandling ();
			}
			targetObject.transform.position = myVisionDataHandling.CameraToWorldCoordinates (new Vector3(visionData.ball [1], ballRadius, visionData.ball [2]));
		} else {
			visionData = null;
		}
			
		ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		if (ms - msPrevious > BotConstants.TimeTickMs) {
			// Below code is executed once per TimeTickMs. This to limit communication to a physical bot.

			// Read input keys. Because we arrive here once in so many calls of FixedUpdate we have to use key events which last long enough.
			float moveHorizontal = Input.GetAxis ("Horizontal");
			float moveVertical = Input.GetAxis ("Vertical");
			bool fPressed = Input.GetKey (KeyCode.F);
			bool bPressed = Input.GetKey (KeyCode.B);
			bool tPressed = Input.GetKey (KeyCode.T);
			bool gPressed = Input.GetKey (KeyCode.G);
			bool leftMouseButtonClicked = Input.GetMouseButton (1);
			// Get mouse position and convert to World coordinates.
			Vector3 mPosition = Input.mousePosition;
			mPosition.z = Camera.main.gameObject.transform.position.y;
			mPosition = Camera.main.ScreenToWorldPoint (mPosition);

			if (leftMouseButtonClicked) {
				// Keep the y position.
				mPosition.y = ballRadius;
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
			} else if (gPressed) {
				// Execute task: Shoot the ball.
				shootTheBall = true;
				isBehindTheBall = false;
			}

			if (gotoTarget) {
				gotoTarget = !GotoWayPoint (targetObject.transform.position);
			} else if (shootTheBall) {
				if (!isBehindTheBall) {
					ballPosition = targetObject.transform.position;
					// Only continue if the ball is not behind the goal.
					if (Math.Abs(ballPosition.x) < Math.Abs(goalPosition.x)) {
						Vector3 fromBehindGoalToBall2D = ballPosition - behindGoalPosition;
						// Make 2D.
						fromBehindGoalToBall2D.y = 0;
						fromBehindGoalToBall2DNorm = fromBehindGoalToBall2D.normalized;
						isBehindTheBall = GotoWayPoint (ballPosition + fromBehindGoalToBall2DNorm * (botLength / 2 + ballRadius + BotConstants.BehindTheBallDistance));
					}
				} else {
					// Now go to the ball to shoot. The bot will be BehindTheBallDistance behind the ball.
					// Make sure BehindTheBallDistance - ShootingDistance is less or equal than RecalculatePathDistance so that the shot can be taken in one move,
					// otherwise the shot will be interrupted by a recalculation.
					isBehindTheBall = !GotoWayPoint (ballPosition + fromBehindGoalToBall2DNorm * (botLength / 2 + ballRadius + BotConstants.ShootingDistance));
					if (!isBehindTheBall) {
						// After shooting drive backwards a bit to make sure:
						// - There is no contact with the ball anymore and the bot does not obscure the ball for the camera. 
						// - The bot stays behind the ball even when the ball moves a bit backwards (e.g. due to a tilted floor).
						myEV3.SendMessage ("Move 0 -20 30", "0");	// Move angle (-180 .. 180) distance (cm) power (0..100)
						msPreviousTask = ms;
					}
				}
			}

			// Receive data from bot and update the bot position in the scene.
			// If vision is connected, the bot position is updated using the vision data.
			string strMessage = myEV3.ReceiveMessage ("EV3_OUTBOX0");
			// Check if the message is valid. The first message received after connecting with the EV3 is "Accept:EV340"
			// indicating that the connection has been established. This is not a valid message.
			if (strMessage != "") {
				string[] data = strMessage.Split (' ');
				if (data.Length == 4) {
					int taskReadyDirect;
					if (int.TryParse (data [0], out taskReadyDirect) && float.TryParse (data [1], out angleMoved) && float.TryParse (data [2], out distanceMoved) && float.TryParse (data [3], out distanceToObject)) {
						// If a new task just has been sent to the bot, the taskReady will still be on '1'.
						// It takes about 3 time ticks of 100 ms to receive taskReady = 0 back from the bot.
						// Therefore we force taskReady to 0 for 5 time ticks after the task has been sent.
						if (ms - msPreviousTask < 5 * BotConstants.TimeTickMs) {
							taskReadyDirect = 0;
						}

						// Delay taskReady going from 0 (not ready) to 1 (ready).
						// This is needed when the taskReady message of a physical bot arrives earlier than the position update of the visionData.
						if (taskReadyDirect == 1) {
							if (taskReadyPrevious == 0) {
								taskReady = 0;
								msStartTaskReadyExtension = ms;
							} else if (ms - msStartTaskReadyExtension > BotConstants.TaskReadyDelay) {
								taskReady = 1;
							}
						} else {
							taskReady = 0;
						}
						taskReadyPrevious = taskReadyDirect;

						float distanceMovedDelta = distanceMoved - distanceMovedPrevious;
						float angleMovedDelta = angleMoved - angleMovedPrevious;
						distanceMovedPrevious = distanceMoved;
						angleMovedPrevious = angleMoved;
						if (calibrated) {
							if (visionData == null) {
								Quaternion rot = Quaternion.Euler (0, angleMovedDelta, 0);
								rb.MoveRotation (rb.rotation * rot);
								rb.MovePosition (rb.transform.position + distanceMovedDelta * rb.transform.forward);
							} else if (visionData.bot1 [0] <= 360) {
								rb.transform.position = myVisionDataHandling.CameraToWorldCoordinates (new Vector3(visionData.bot1 [1], botHeight, visionData.bot1 [2]));;
								rb.rotation = Quaternion.Euler (0, visionData.bot1 [0], 0);
							}
						}
						calibrated = true;
						if (!myEV3.simOnly && false) {
							// Draw a wall in front of the bot when an object is detected.
							// Do not do this in simulation mode as this wall might collide with the bot at distance 0.
							var wallObject = GameObject.Find ("Wall");
							MeshRenderer renderWall = wallObject.GetComponentInChildren<MeshRenderer> ();
							Vector3 vec = new Vector3 ();
							vec.x = rb.transform.position.x;
							vec.y = wallObject.transform.position.y; // Original height.
							vec.z = rb.transform.position.z;
							wallObject.transform.position = vec;
							wallObject.transform.rotation = rb.transform.rotation;
							// Place the wall in front of the bot at distanceToObject units.
							wallObject.transform.Translate (0, 0, distanceToObject + 10); // +10 to compensate for bot length.
							if (distanceToObject >= 0 && distanceToObject < 30) {
								renderWall.enabled = true;
							} else {
								renderWall.enabled = false;
							}
						}

					}
				}
			}
			msPrevious = ms;
		}
	}


	// Move the specified position.
	bool GotoWayPoint (Vector3 position)
	{
		bool finished = false;
		// Make heigth of source and destination zero else the distance calculations below go wrong.
		Vector3 botPosition = rb.transform.position;
		position.y = 0;
		botPosition.y = 0;
		if (taskReady == 1) {
			NavMeshPath path = new NavMeshPath ();
			bool result = NavMesh.CalculatePath (botPosition, position, NavMesh.AllAreas, path);
			if (result && path.corners.Length > 1) {
				if (Vector3.Distance (botPosition, position) > BotConstants.WayPointAccuracy) {
					float targetDistance = Math.Min (BotConstants.RecalculatePathDistance, Vector3.Distance (botPosition, path.corners [1]));
					Quaternion rotPlus90 = Quaternion.Euler (0, 90, 0);
					Quaternion rotMin90 = Quaternion.Euler (0, -90, 0);
					var relativePos = path.corners [1] - botPosition;
					var relativeRot = Quaternion.LookRotation (relativePos);
					float targetAngle = Quaternion.Angle (rb.transform.rotation, relativeRot);
					float targetAnglePlus90 = Quaternion.Angle (rb.transform.rotation * rotPlus90, relativeRot);
					float targetAngleMin90 = Quaternion.Angle (rb.transform.rotation * rotMin90, relativeRot);
					targetAngle = targetAnglePlus90 < targetAngleMin90 ? targetAngle : -targetAngle;
					myEV3.SendMessage ("Move " + targetAngle.ToString () + " " + targetDistance.ToString () + " 30", "0"); // Move angle (-180 .. 180) distance (cm) power (0..100)
					msPreviousTask = ms;
					lineRenderer.positionCount = path.corners.Length;
					for (int i = 0; i < path.corners.Length; i++) {
						lineRenderer.SetPosition (i, path.corners [i]);
					}
				} else {
					lineRenderer.positionCount = 0;
					finished = true;
				}
			}

		}
		return finished;
	}

	void OnGUI ()
	{
		GUIStyle styleTextField = new GUIStyle (GUI.skin.textField);
		styleTextField.fontSize = 12;
		ipAddress = GUILayout.TextField (ipAddress, styleTextField);
		GUIStyle styleButton = new GUIStyle (GUI.skin.button);
		styleButton.fontSize = 12;
		if (myEV3.isConnected == false) {
			styleButton.normal.textColor = UnityEngine.Color.red;
			if (GUILayout.Button ("Connect EV3", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiConnectEV3 = true;
			}
		} else if (myEV3.isConnected == true) {
			styleButton.normal.textColor = UnityEngine.Color.green;
			if (GUILayout.Button ("Disconnect EV3", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiDisconnectEV3 = true;
			}
		}
		if (myVision.isConnected == false) {
			styleButton.normal.textColor = UnityEngine.Color.red;
			if (GUILayout.Button ("Connect vision", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiConnectVision = true;
			}
		} else if (myVision.isConnected == true) {
			styleButton.normal.textColor = UnityEngine.Color.green;
			if (GUILayout.Button ("Disconnect vision", styleButton, GUILayout.Width (100), GUILayout.Height (25))) {
				guiDisconnectVision = true;
			}
		}

		styleButton.normal.textColor = UnityEngine.Color.red;
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
		if (myVision.isConnected) {
			myEV3.Disconnect ();
		}
		if (myVision.isConnected) {
			myVision.Disconnect ();
		}
	}
}


// Class used by JsonConvert.DeserializeObject to deserialize the JSON data received from the vision server.
public class VisionData
{    
	public float[] videoSize = { 0.0f, 0.0f };
	public float[] bot1 = { 0.0f, 0.0f, 0.0f };
	public float[] ball = { 0.0f, 0.0f, 0.0f };
}


// Class for handle / calibrate vision data.
public class VisionDataHandling
{
	private GameObject bot;
	private VisionData visionData;
	private float xMinWorld, xMaxWorld, zMinWorld, zMaxWorld;
	private float xMinCamera, xMaxCamera, zMinCamera, zMaxCamera;

	public VisionDataHandling()
	{
		// Get the Unity world dimensions.
		bot = GameObject.Find ("Bot");
		var groundObject = GameObject.Find ("Ground");
		xMinWorld = groundObject.GetComponent<Renderer> ().bounds.min.x;
		xMaxWorld = groundObject.GetComponent<Renderer> ().bounds.max.x;
		zMinWorld = groundObject.GetComponent<Renderer> ().bounds.min.z;
		zMaxWorld = groundObject.GetComponent<Renderer> ().bounds.max.z;

		visionData = bot.GetComponent<BotController> ().visionData;
		// Crop the camera image to match the beamer projection.
		var marginCameraX = visionData.videoSize [0] * VisionConstants.CameraMargin;
		var marginCameraZ = visionData.videoSize [1] * VisionConstants.CameraMargin;
		xMinCamera = marginCameraX;
		xMaxCamera = visionData.videoSize [0] - marginCameraX;
		zMinCamera = marginCameraZ;
		zMaxCamera = visionData.videoSize [1] - marginCameraZ;
	}

	public Vector3 CameraToWorldCoordinates(Vector3 pVision)
	{
		// Map the camera coordinates to the Unity world coordintes.
		// Camera coordinates:
		// Top left = (x,z) = (0,0).
		// x increases from left to right and z increases from top to bottom.
		// Unity world coordinates:
		// The middle of the Unity ground is (x,z) = (0,0).
		// x increases from left to right and z increases from bottom to top.
		Vector3 pWorld = new Vector3();
		pWorld.x = map (pVision.x, xMinCamera, xMaxCamera, xMinWorld, xMaxWorld);
		pWorld.y = pVision.y;
		pWorld.z = map (pVision.z, zMaxCamera, zMinCamera, zMinWorld, zMaxWorld);
		// Compensate for the heigth of the detected video feature.
		float tanX = pWorld.x / VisionConstants.CameraHeight;
		float tanZ = pWorld.z / VisionConstants.CameraHeight;
		pWorld.x = pWorld.x - pVision.y * tanX;
		pWorld.z = pWorld.z - pVision.y * tanZ;
		return pWorld;
	}

	private float map (float s, float a1, float a2, float b1, float b2)
	{
		return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
	}
}


// Class for communication to EV3 or simulation. The simulation mimics the EV3 with regard to sending and receiving messages.
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
	private float angleToMovePerTimeTick, distanceToMovePerTimeTick;
	private float angleToMove, distanceToMove, pwr;
	// To indicate bot is ready for the next task
	private int taskReady = 1;

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
		// Store msg in lastMessageSent so it can ve handled in ReceiveMessageSim to simulate bot movement.
		lastMessageSent = msg;
	}

	// ReceiveMessageSim simulates receiving a message from the bot.
	// It uses the message sent with SendMessageSim to determine the movement of the bot.
	// This message contains a distanceToMove, angleToMove and pwr.
	// The simulated bot first turns by angleToMove degrees and then moves straight for distanceToMove units with power pwr.
	private string ReceiveMessageSim (string mbox)
	{
		if (newMessageArrived) {
			string[] messageStrings = lastMessageSent.Split (' ');
			if (messageStrings [0] == "Move" && float.TryParse (messageStrings [1], out angleToMove) && float.TryParse (messageStrings [2], out distanceToMove) && float.TryParse (messageStrings [3], out pwr)) {
				angleToMovePerTimeTick = pwr * (angleToMove > 0 ? BotConstants.PowerToAnglePerTimeTick : -BotConstants.PowerToAnglePerTimeTick);
				distanceToMovePerTimeTick = pwr * (distanceToMove > 0 ? BotConstants.PowerToDistancePerTimeTick : -BotConstants.PowerToDistancePerTimeTick);
				angleToMoveRemaining = angleToMove != 0 ? Math.Abs (angleToMove) - Math.Abs (angleToMovePerTimeTick) : 0;
				distanceToMoveRemaining = distanceToMove != 0 ? Math.Abs (distanceToMove) - Math.Abs (distanceToMovePerTimeTick) : 0;
				newMessageArrived = false;
				taskReady = 0; // Indicate bot is not ready for the next task.
			} else if (messageStrings [0] == "Reset") {
				angleMoved = 0.0f;
				distanceMoved = 0.0f;
				taskReady = 1; // Indicate bot is ready for the next task.
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
			taskReady = 1; // Indicate bot is ready for the next task.
		}
		return taskReady.ToString () + " " + angleMoved.ToString () + " " + distanceMoved.ToString () + " " + distanceToObject.ToString ();
	}
}
