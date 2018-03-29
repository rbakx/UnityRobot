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


task main()
{
	displayBigTextLine(0, "Started!");

	char msgBufIn[MAX_MSG_LENGTH];  // To contain the incoming message.
	char msgBufOut[MAX_MSG_LENGTH];  // To contain the outgoing message
	float speed, direction, distance, angle;

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
			if (strncmp(msgBufIn, "Drive", strlen("Drive")) == 0)
			{
				sscanf(msgBufIn, "Drive %f %f %f", &speed, &direction, &distance);
				if (distance > 0.0)
				{
					setMotorSyncEncoder(motorB, motorC, direction, (distance / 17.6) * 360.0, speed);
				}
				else
				{
					setMotorSync(motorB, motorC, direction, speed);
				}
			}
			else if (strncmp(msgBufIn, "Turn", strlen("Turn")) == 0)
			{
				float angleMeasured = getGyroDegrees(S2);
				sscanf(msgBufIn, "Turn %f %f", &speed, &angle);
				float angleTarget = angleMeasured + angle;
				if (angle > 0) // Right turn.
				{
					setMotorSync(motorB, motorC, -100, speed);
					while(angleMeasured < angleTarget)
					{
						angleMeasured = getGyroDegrees(S2);
					}
				}
				else if (angle < 0) // Left turn.
				{
					setMotorSync(motorB, motorC, 100, speed);
					while(angleMeasured > angleTarget)
					{
						angleMeasured = getGyroDegrees(S2);
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

		float dist = getUSDistance(S4);
		float angle = getGyroDegrees(S2);
		int encoderB = getMotorEncoder(motorB);
		int encoderC = getMotorEncoder(motorC);
		sprintf(msgBufOut, "%.1f %.1f %d %d", dist, angle, encoderB, encoderC);
		writeMailboxOut("EV3_OUTBOX0", msgBufOut);
		delay(100);  // Wait 100 ms to give host computer time to react.
	}
	closeMailboxIn("EV3_INBOX0");
	closeMailboxOut("EV3_OUTBOX0");
	return;
}
