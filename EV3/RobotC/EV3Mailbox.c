////////////////////////////////////////////////////////////////////////////////
// This file contains the mailbox helper functions.
// LIMITATIONS:
// Use only EV3_INBOX0..EV3_INBOX9 and EV3_OUTBOX0, so max. 10 input mailboxes and 1 output mailbox.
// DEVELOPER INFO:
// Receiving messages from a host computer is done using the ROBOTC mailbox functionality.
// This functionality is limited with respect to mailbox names:
// opening a mailbox must be done using index 0..9 and a corresponding name which is the index as a string.
// Subsequently reading a message is done using the mailbox index.
// Sending messages to a host computer is done by using file I/O, inspired by
// https://siouxnetontrack.wordpress.com/2013/09/27/connecting-the-pc-to-our-ev3/
// Also this functionality is limited. Because file I/O is used the EV3 writes to a local file and
// the host computer reads this file using the opFile direct command containing the name of the file.
// This asynchrounous call triggers the next read from the file and returns the message of the previous read.
// The message received does not contain the name of the file so using multiple files to mimic multiple mailboxes
// is not possible right now. If this is needed, the name of the file should also be part of the message.
////////////////////////////////////////////////////////////////////////////////

#define MAX_MAILBOXNAME_LENGTH 20
#define MAX_MSG_LENGTH 32


// Function: Open input mailbox.
// Parameters: name of the mailbox to open.
// Return value: void.
void openMailboxIn(char *name)
{
	// To keep it simple, we only use mailbox 0.
	// Because of a ROBOTC limitation, the name must be equal to the index as a string.
	int index;
	char internal_name[MAX_MAILBOXNAME_LENGTH];
	sscanf(name, "EV3_INBOX%d", &index);
	sscanf(name, "EV3_INBOX%s", internal_name);
	if (index >=0 && index < 10)
	{
		openMailbox(index, internal_name);
	}
}


// Function: Close input mailbox.
// Parameters: name of the mailbox to close.
// Return value: void.
void closeMailboxIn(char *name)
{
}


// Function: Read input mailbox. Non-blocking function.
// Parameters:
//   name: the name of the mailbox to read
// Return value: message read as a string. If no message available, an empty string is returned.
void readMailboxIn(char *name, char *msg)
{
	int index = -1;
	int size;
	strcpy(msg, "");
	sscanf(name, "EV3_INBOX%d", &index);
	if (index >=0 && index < 10)
	{
		size = getMailboxMessageSize(index);
		if (size > 0)
		{
			readMailbox(index, msg, MAX_MSG_LENGTH);
		}
	}
}


// Function: Open output mailbox. This function actually creates the associated file.
// Parameters:
//   mailboxName: Name of the mailbox.
//                ATTENTION: mailboxName must not be longer than 16 characters,
//                otherwise the associated filename (mailboxName + ".rtf") will be truncated!
// Return value: void
void openMailboxOut(char *mailboxName)
{
	long fpMailBox;
	char fullName[25];
	strcpy(fullName, mailboxName);
	strcat(fullName, ".rtf");
	fpMailBox = fileOpenWrite(fullName);
	fileWriteData(fpMailBox, "", 1);  // Write empty file.
	fileClose(fpMailBox);}


// Function:
//   Close input mailbox.
// No special action is performed as the associated mailbox file is already closed after every write action.
//		 It would be nice to be able to delete the associated mailbox file, but this is not possible from ROBOTC.
// Parameters:
//   mailboxName: Name of the mailbox.
//                ATTENTION: mailboxName must not be longer than 16 characters,
//                otherwise the associated filename (mailboxName + ".rtf") will be truncated!
// Return value: void
void closeMailboxOut(char *mailboxName)
{
}


// Function:
//   Write output mailbox. This is done using file I/O.
// Parameters:
//   mailboxName: Name of the mailbox.
//                ATTENTION: mailboxName must not be longer than 16 characters,
//                otherwise the associated filename (mailboxName + ".rtf") will be truncated!
//   msg:         The message to write.
// Return value: void
void writeMailboxOut(char *mailboxName, char *msg)
{
	long fpMailBox;
	char fullName[25];
	strcpy(fullName, mailboxName);
	strcat(fullName, ".rtf");
	fpMailBox = fileOpenWrite(fullName);
	fileWriteData(fpMailBox, msg, strlen(msg)+1);  // +1 because of termination character?
	fileClose(fpMailBox);
}
