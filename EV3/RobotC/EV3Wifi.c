// RobotC demonstration program.
// This program receives messages from the host: speed and status requests.
// It sets the requested motor speed and sends back the measured ultrasonic sensor distance value.
// Wifi communication is used.
#define MAX_MSG_LENGTH 32

// Reads a message from the mailbox with index 'index'.
// The message is read into 'msg'.
void readMessage(int index, char* msg)
{
	int msgSize;

	msgSize = getMailboxMessageSize(index);
	if (msgSize > 0)
	{
		readMailbox(index, msg, MAX_MSG_LENGTH);
	}
	else
	{
		msg = "";
	}
}

task main()
{
	displayBigTextLine(0, "Started!");

	long fpDist;
	char msgBuf[MAX_MSG_LENGTH];  // to contain the incoming message.

	// Below the filename to communicate with the host.
	// This file will appear at the host side at '../prjs/rc-data/DISTANCE.rtf'.
	// This means the host must use '../prjs/rc-data/DISTANCE.rtf' as filename in the Direct Command to access this file.
	// ATTENTION: the filename (here 'DISTANCE.rtf') must not be longer than 20 characters, otherwise it will be truncated!
	string sFileName = "DISTANCE.rtf"; // file name max. 20 characters long!

	openMailbox(0, "0"); // mailbox for receiving STATUS request.
	openMailbox(1, "1"); // mailbox for receiving SPEED request.
	while (true)
	{
		// Check if there is a request for measuring the distance.
		readMessage(0, msgBuf);
		if (strcmp(msgBuf, "get_distance") == 0)
		{
			float dist;
			char strDist[10];
			dist =  getUSDistance(S4);  // Ultrasonic sensor in port 4.
			// Convert the measured sistance to a string.
			sprintf(strDist, "%.1f", dist);
			// Write distanse as a string to the distance file.
			fpDist = fileOpenWrite(sFileName);
			fileWriteData(fpDist, strDist, strlen(strDist)+1);  // +1 because of termination character?
			fileClose(fpDist);
		}

		// Check if there is a speed request.
		readMessage(1, msgBuf);
		if (strcmp(msgBuf, "") != 0)
		{
			float speed;
			speed = atof(msgBuf);
			setMotorSpeed(motorB, speed);
			setMotorSpeed(motorC, speed);
		}
		sleep(100);
	}
	closeMailbox(0);
	closeMailbox(1);
	return;
}
