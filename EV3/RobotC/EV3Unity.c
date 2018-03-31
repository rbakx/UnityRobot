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
#define DEGREES_TO_ENCODER_MULT_FACTOR 2.8 // 1.5 * 360 encoder ticks for 360 degree turn.


task main()
{
	displayBigTextLine(0, "Started!");

	char msgBufIn[MAX_MSG_LENGTH];  // To contain the incoming message.
	char msgBufOut[MAX_MSG_LENGTH];  // To contain the outgoing message
	float pwr, direction, distanceToMove, angleToTurn;

	openMailboxIn("EV3_INBOX0");
	openMailboxOut("EV3_OUTBOX0");

	resetMotorEncoder(motorB);
	resetMotorEncoder(motorC);
	resetGyro(S2);
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
				sscanf(msgBufIn, "Move %f %f %f", &pwr, &direction, &distanceToMove);
				setMotorSyncEncoder(motorB, motorC, direction, distanceToMove * DISTANCE_TO_ENCODER_MULT_FACTOR, pwr * sgn(distanceToMove));
			}
			else if (strncmp(msgBufIn, "Turn", strlen("Turn")) == 0)
			{
				// We use setMotorSyncEncoder also for turning so we do not need a blocking while loop
				// to check the gyro angle.
				sscanf(msgBufIn, "Turn %f %f", &pwr, &angleToTurn);
				setMotorSyncEncoder(motorB, motorC, -100, angleToTurn * DEGREES_TO_ENCODER_MULT_FACTOR, pwr * sgn(angleToTurn));
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

		float distanceMoved = (getMotorEncoder(motorB) + getMotorEncoder(motorC)) * ENCODER_TO_DISTANCE_MULT_FACTOR / 2.0;
		float angleTurned = getGyroDegrees(S2);
		float distanceToObject = getUSDistance(S4);
		sprintf(msgBufOut, "%.1f %.1f %.1f", distanceMoved, angleTurned, distanceToObject);
		writeMailboxOut("EV3_OUTBOX0", msgBufOut);
		delay(100);  // Wait 100 ms to give host computer time to react.
	}
	closeMailboxIn("EV3_INBOX0");
	closeMailboxOut("EV3_OUTBOX0");
	return;
}
