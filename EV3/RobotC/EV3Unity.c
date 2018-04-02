////////////////////////////////////////////////////////////////////////////////
// ROBOTC EV3 remote control demonstration program.
// This program receives messages from the host computer: Forward, Backward, Left and Right.
// It sends back some semsor values.
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Connection scheme:
// motorA: medium motor front lever
// motorB: large motor right wheel
// motorC: large motor left wheel
// motorD: -
// Sensor S1: Touch sensor
// Sensor S2: Gyro sensor
// Sensor S3: Color sensor
// Sensor S4: Ultrasonic sensor
////////////////////////////////////////////////////////////////////////////////


// EV3Mailbox.c is needed to enable sending and receiving mailbox messages.
// It is possible to have ten input mailboxes and one output mailbox,
// however in the below code only EV3_INBOX0 and EV3_OUTBOX0 are used.
// See the comments in EV3Mailbox.c for additional info.
// Unfortunately ROBOTC cannot handle multipe C files so we have to include it.
#include "EV3Mailbox.c"

#define ENCODER_TO_DISTANCE_MULT_FACTOR 0.04889 // 17.6 / 360.0
#define DISTANCE_TO_ENCODER_MULT_FACTOR 20.45 // 360.0 / 17.6

// Global variables
char doMove_msgBufIn[MAX_MSG_LENGTH];  // To contain the incoming message for the doMove task.
int taskReady; // To indicate EV3 is ready for the next task.


// Task for moving.
task doMove()
{
	float angleToMove, distanceToMove, pwr;
	sscanf(doMove_msgBufIn, "Move %f %f %f", &angleToMove, &distanceToMove, &pwr);
	taskReady = 0; // Indicate EV3 is not ready for the next task.
	if (angleToMove != 0)
	{
		float angleMeasured = getGyroDegrees(S2);
		// Determine a correction angle dependant of pwr to prevent overshoot while turning.
		// This is the best we can do at the moment and empirically determined.
		float angleCorrection = pwr / 3.0;
		if (angleCorrection < abs(angleToMove))
		{
			angleToMove = angleToMove - sgn(angleToMove) * angleCorrection;
		}
		else // For small angles and high pwr the correction angle gets too large so just take a fraction of angleToMove.
		{
			angleToMove = angleToMove / 3.0;
		}
		float angleTarget = angleMeasured + angleToMove;
		setMotorSync(motorB, motorC, -100, pwr * sgn(angleToMove));
		// If angleToMove is positive it is a right turn.
		if (angleToMove > 0) // Right turn.
		{
			while(angleMeasured < angleTarget)
			{
				angleMeasured = getGyroDegrees(S2);
			}
		}
		// If angleToMove is negative it is a left turn.
		else // Left turn.
		{
			while(angleMeasured > angleTarget)
			{
				angleMeasured = getGyroDegrees(S2);
			}
		}
		// Stop turning.
		setMotorSync(motorB, motorC, 0, 0);
	}
	if (distanceToMove != 0)
	{
		setMotorSyncEncoder(motorB, motorC, 0, distanceToMove * DISTANCE_TO_ENCODER_MULT_FACTOR, pwr * sgn(distanceToMove));
		waitUntilMotorStop(motorB);
	}
	taskReady = 1; // Indicate EV3 is ready for the next task.
}


// Main task
task main()
{
	displayBigTextLine(0, "Started!");

	char msgBufIn[MAX_MSG_LENGTH];  // To contain the incoming message.
	char msgBufOut[MAX_MSG_LENGTH];  // To contain the outgoing message

	openMailboxIn("EV3_INBOX0");
	openMailboxOut("EV3_OUTBOX0");

	resetMotorEncoder(motorB);
	resetMotorEncoder(motorC);
	resetGyro(S2);
	taskReady = 1; // Indicate EV3 is ready for the next task.
	while (true)
	{
		// Read input message.
		// readMailboxIn() is non-blocking and returns "" if there is no message.
		readMailboxIn("EV3_INBOX0", msgBufIn);
		if (strcmp(msgBufIn, "") != 0)
		{
			displayBigTextLine(4, msgBufIn);
			if (strncmp(msgBufIn, "Move", strlen("Move")) == 0)
			{
				// Handle Move in a separate task, so that the main task can report the back the status in parallel.
				strcpy(doMove_msgBufIn, msgBufIn); // Copy input message to input message for doTurn task.
				startTask(doMove, kDefaultTaskPriority);
			}
			else
			{
				displayBigTextLine(4, "invalid message!");
			}
		}
		else
		{
			//displayBigTextLine(4, "empty message!");
		}

		float angleMoved = getGyroDegrees(S2);
		float distanceMoved = (getMotorEncoder(motorB) + getMotorEncoder(motorC)) * ENCODER_TO_DISTANCE_MULT_FACTOR / 2.0;
		float distanceToObject = getUSDistance(S4);
		sprintf(msgBufOut, "%d %.1f %.1f %.1f", taskReady, angleMoved, distanceMoved, distanceToObject);
		writeMailboxOut("EV3_OUTBOX0", msgBufOut);
		delay(100);  // Wait 100 ms to give host computer time to react.
	}
	closeMailboxIn("EV3_INBOX0");
	closeMailboxOut("EV3_OUTBOX0");
	return;
}
