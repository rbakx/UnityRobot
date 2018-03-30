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

#define DEGREES_TO_CM_MULT_FACTOR 0.04889 // 17.6 / 360.0
#define CM_TO_DEGREES_MULT_FACTOR 20.45 // 360.0 / 17.6


task main()
{
	displayBigTextLine(0, "Started!");

	char msgBufIn[MAX_MSG_LENGTH];  // To contain the incoming message.
	char msgBufOut[MAX_MSG_LENGTH];  // To contain the outgoing message
	float pwr, direction, distanceToMove, degreesToTurn;

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
				if (distanceToMove > 0.0)
				{
					setMotorSyncEncoder(motorB, motorC, direction, distanceToMove * CM_TO_DEGREES_MULT_FACTOR, pwr);
				}
				else if (distanceToMove < 0.0)
				{
					setMotorSyncEncoder(motorB, motorC, direction, distanceToMove * CM_TO_DEGREES_MULT_FACTOR, -pwr);
				}
				else
				{
					setMotorSync(motorB, motorC, direction, pwr);
				}
			}
			else if (strncmp(msgBufIn, "Turn", strlen("Turn")) == 0)
			{
				float degreesMeasured = getGyroDegrees(S2);
				sscanf(msgBufIn, "Turn %f %f", &pwr, &degreesToTurn);
				float degreesTarget = degreesMeasured + degreesToTurn;
				if (degreesToTurn > 0) // Right turn.
				{
					setMotorSync(motorB, motorC, -100, pwr);
					while(degreesMeasured < degreesTarget)
					{
						degreesMeasured = getGyroDegrees(S2);
					}
				}
				else if (degreesToTurn < 0) // Left turn.
				{
					setMotorSync(motorB, motorC, 100, pwr);
					while(degreesMeasured > degreesTarget)
					{
						degreesMeasured = getGyroDegrees(S2);
					}
				}
				// Stop turning.
				setMotorSync(motorB, motorC, 0, 0);
			}
		}
		else
		{
			//displayBigTextLine(4, "empty message!");
		}

		float distanceMoved = (getMotorEncoder(motorB) + getMotorEncoder(motorC)) * DEGREES_TO_CM_MULT_FACTOR / 2.0;
		float degreesTurned = getGyroDegrees(S2);
		float distanceToObject = getUSDistance(S4);
		sprintf(msgBufOut, "%.1f %.1f %.1f", distanceMoved, degreesTurned, distanceToObject);
		writeMailboxOut("EV3_OUTBOX0", msgBufOut);
		delay(100);  // Wait 100 ms to give host computer time to react.
	}
	closeMailboxIn("EV3_INBOX0");
	closeMailboxOut("EV3_OUTBOX0");
	return;
}
