using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace EV3WifiLib
{
    // State object for receiving TCP data from remote device.
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    // Class to communicate with EV3 via Wifi.
    public class EV3Wifi
    {
        public IPEndPoint target;
        public String serialNumber;
        private UdpClient udpSocket;
        private IPEndPoint source;
        private Socket tcpSocket;
        // Array to keep the UDP message which is filled by the asynchronous callback method.
        private byte[] message = new byte[256];
        // The response from the remote device.
        private String response = String.Empty;

        // ManualResetEvent instances signal completion.
        private ManualResetEvent connectDone = new ManualResetEvent(false);
        private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);

        // Make connection to EV3.
        public String Connect()
        {
            try
            {
                String status = NegotiateUdp();
                if (status != "ok")
                {
                    return status;
                }
                return (StartTCPClient());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return e.ToString();
            }
        }

        // Disconnect, meaning stopping the TCP client.
        public void Disconnect()
        {
            // Disconnect from remote device.
            try
            {
                // Release the socket.
                tcpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // UDP data callback method.
        private void OnUdpData(IAsyncResult result)
        {
            // This is what had been passed into BeginReceive as the second parameter.
            try
            {
                UdpClient udpsocket = result.AsyncState as UdpClient;
                // Get the actual message and fill out the source.
                message = udpsocket.EndReceive(result, ref source);
                // Schedule the next receivsee operation once reading is done.
                udpsocket.BeginReceive(new AsyncCallback(OnUdpData), udpsocket);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Wait for UDP broadcast of EV3 and respond to EV3 so the EV3 will accept TCP connection requests.
        private String NegotiateUdp()
        {
            // Creates a UdpClient for reading incoming data.
            // When no port number specified the UdpClient will automatically pick an available port number as the source port.
            try
            {
                udpSocket = new UdpClient(3015);  // listen for a UDP broadcast from the EV3 on port 3015
                // Schedule the first receive operation.
                udpSocket.BeginReceive(new AsyncCallback(OnUdpData), udpSocket);

                Boolean UdpConfirmed = false;
                // Busy waiting until returned message contains a valid Serial number.
                while (UdpConfirmed == false)
                {
                    String msgStr = Encoding.ASCII.GetString(message, 0, message.Length);
                    Regex regex = new Regex("Serial-Number: (.*)");
                    Match match = regex.Match(msgStr);
                    if (match.Success)
                    {
                        serialNumber = match.Groups[1].Value;
                        byte[] msg = Encoding.ASCII.GetBytes("hi");
                        udpSocket.Send(msg, msg.Length, source);
                        UdpConfirmed = true;
                    }
                }
                return "ok";
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return e.ToString();
            }
        }

        // Send a TCP connection request to th eEV3. The IP address is known from the previous UDP broadcast.
        private String StartTCPClient()
        {
            // Connect to a remote device.
            try
            {
                // Create a TCP/IP socket.
                tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.

                target = new IPEndPoint(source.Address, 5555);
                tcpSocket.BeginConnect(target, new AsyncCallback(ConnectCallback), tcpSocket);
                connectDone.WaitOne();

                // Send test data to the remote device.
                String str = "GET /target?sn=" + serialNumber + " VMTP1.0\nProtocol: EV3";
                Send(tcpSocket, str);

                // Receive the responses from the remote device.
                StartReceive(tcpSocket);
                receiveDone.WaitOne();
                return "ok";
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return e.ToString();
            }
        }

        // Callback method for connection request.
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Start receiving TCP data.
        private void StartReceive(Socket client)
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Send message containing a string to the EV3.
        public void SendMessage(String msg, String mbox)
        {
            try
            {
                // Send message using System Command 'WRITEMAILBOX'.
                byte[] byteArray;
                int len = 11 + msg.Length + mbox.Length;
                byteArray = new byte[len];
                // See the EV3 System Command documentation for the definition of the bytes.
                // byte 0..1: length bytes little endian (LSB on lowest address so on byte 0).
                // byte 2..3: message counter, not used, set to 0x00.
                // byte 4: command type: 0x81 = SYSTEM_COMMAND_NO_REPLY.
                // byte 5: System Command: 0x9E = WRITEMAILBOX.
                // byte 6: length of mailbox name including null termination character.
                // byte 7..n: mailbox name.
                // byte n+1: null termination character: '\0'.
                // byte n+2,n+3: length of message little endian (LSB on lowest address so on byte n+2).
                // byte n+4..n+m: message including null termination character: '\0'.
                copyArray(byteArray, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x81, 0x9E, 0x00 }, 0);
                copyArray(byteArray, Encoding.ASCII.GetBytes(mbox), 7);
                copyArray(byteArray, new byte[] { (byte) '\0' }, 7 + mbox.Length);
                copyArray(byteArray, Encoding.ASCII.GetBytes(msg), 7 + mbox.Length + 3);
                copyArray(byteArray, new byte[] { (byte) '\0' }, 7 + mbox.Length + 3 + msg.Length);
                byteArray[0] = (byte) (len - 2);  // length of array excluding the two length bytes
                byteArray[6] = (byte) (mbox.Length + 1);  // length of mailbox name
                byteArray[7 + mbox.Length + 1] = (byte) (msg.Length + 1);  // length of message
                Send(tcpSocket, byteArray);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Send message containing a 4 byte IEEE 754 single-precision binary floating-point to the EV3.
        public void SendMessage(float msg, String mbox)
        {
            try
            {
                // Send message using System Command 'WRITEMAILBOX'.
                byte[] byteArray;
                int len = 14 + mbox.Length;
                byteArray = new byte[len];
                // See the EV3 System Command documentation for the definition of the bytes.
                // byte 0..1: length bytes little endian (LSB on lowest address so on byte 0).
                // byte 2..3: message counter, not used, set to 0x00.
                // byte 4: command type: 0x81 = SYSTEM_COMMAND_NO_REPLY.
                // byte 5: System Command: 0x9E = WRITEMAILBOX.
                // byte 6: length of mailbox name including null termination character.
                // byte 7..n: mailbox name.
                // byte n+1: null termination character: '\0'.
                // byte n+2,n+3: length of message little endian (LSB on lowest address so on byte n+2).
                // byte n+4..n+8: message containing 4 bytes representing a IEEE 754 single-precision binary floating-point.
                // This format is used by the EV3 when the mailbox receives a numerical value.
                copyArray(byteArray, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x81, 0x9E, 0x00 }, 0);
                copyArray(byteArray, Encoding.ASCII.GetBytes(mbox), 7);
                copyArray(byteArray, new byte[] { (byte) '\0' }, 7 + mbox.Length);
                copyArray(byteArray, BitConverter.GetBytes(msg), 7 + mbox.Length + 3); // 4 byte IEEE 754 single-precision binary floating-point
                byteArray[0] = (byte) (len - 2);  // length of array excluding the two length bytes
                byteArray[6] = (byte) (mbox.Length + 1);  // length of mailbox name
                byteArray[7 + mbox.Length + 1] = 4;  // length of message
                Send(tcpSocket, byteArray);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Receive message from EV3.
        public String ReceiveMessage(String project, String mbox)
        {
            try
            {
                // Retrieve the response string.
                String tmpResponse = response;
                response = "";  // clear response to indicate it is handled
                // Initiate the next message retrieval from the EV3.
                startReceiveMessage(project, mbox);
                return tmpResponse;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return "";
            }
        }

        // Callback method for receicing TCP data.
        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket .
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
                String tmpResponse = "";

                // Read data from the remote device. 
                int bytesRead = client.EndReceive(ar);
                // The opFile Direct Command that is used will return 256 bytes.
                if (bytesRead == 256)
                {
                    // state.buffer[0..255] now contain the Direct Command reply bytes.
                    // See the EV3 Direct Command documentation for the definition of the bytes.
                    // byte 0..1: length bytes little endian (LSB on lowest address so on byte 0).
                    // byte 2..3: message counter, not used.
                    // byte 4: reply type: 0x00 =  DIRECT_COMMAND_REPLY.
                    // byte 5..n: reponse buffer which are the global variables reserved in the Direct Command.
                    // In the opFile Direct Command we reserved 251 global variables starting at offset 0 (index 5).
                    // The file length is at global variable offset 4 so at index 9.
                    // The start of the string read is at global variable offset 8 so at index 13.

                    // Extract the text from the response.
                    // If the file is not found, the length of the file = state.buffer[9] will be set to 0.
                    // In this case, leave tmpResponse = "" so the previous response will not be overwritten.
                    if (state.buffer[9] != 0)
                    {
                        tmpResponse = Encoding.ASCII.GetString(state.buffer, 13, state.buffer[9] - 1);  // -1 because of trailing 0xD = carriage return.
                    }
                    else
                    {
                        tmpResponse = "EV3 Message out box not found";
                    }
                }
                else
                {
                    // For any other response, just return the complete response.
                    tmpResponse = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                }
                // Only overwrite response if state.buffer contains a valid string.
                // This check is needed because for example the opFile(CLOSE) command will give an empty response.
                // opFile(CLOSE) is issued right after opFile(READ_TEXT) so it would otherwise overwrite the
                // response on opFile(READ_TEXT).
                // The check is done by cheking the first two length bytes which should not be 0.
                if (tmpResponse != "" && (state.buffer[0] != 0 || state.buffer[0] != 0))
                {
                    response = tmpResponse;
                }
                // Continue receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                receiveDone.Set();
            }
            catch (Exception e)
            {
                response = e.ToString();
                Console.WriteLine(e.ToString());
            }
        }

        // Send TCP string data.
        private void Send(Socket client, String data)
        {
            try
            {
                // Convert the string data to byte data using ASCII encoding.
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data to the remote device.
                client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Send TCP byte data.
        private void Send(Socket client, byte[] byteData)
        {
            try
            {
                // Begin sending the data to the remote device.
                client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Callback method for sending data.
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Start receiving a message.
        // This is done by reading a file on the EV3 using the opFile command packed in a Direct Command.
        private void startReceiveMessage(String project, String mbox)
        {
            try
            {
                //String fileName = "../Projects/" + project + "/" + mbox + ".rtf";
                String fileName = "../prjs/" + project + "/" + mbox + ".rtf";
                byte[] byteArray;
                int len = 22 + fileName.Length;
                byteArray = new byte[len];
                // See https://siouxnetontrack.wordpress.com/2014/08/19/sending-data-over-wifi-between-our-pc-application-and-the-ev3-part-1/
                // and the EV3 Direct Command documentation for the definition of the bytes.
                // byte 0..1: length bytes little endian (LSB on lowest address so on byte 0).
                // byte 2..3: message counter, not used, set to 0x00.
                // byte 4: command type: 0x00 = DIRECT_COMMAND_REPLY.
                // byte 5..6: number of globals and locals reserved: 0xFB = 251 global variables, to come to a reply of 256 bytes..
                // Global variables are use to contain parameters and the return value.
                // byte 7: byte code of command: 0xC0 = opFile command.
                // byte 8: subcommand: 0x01 = OPEN_READ.
                // byte 9: LCS (Local Constant String) meaning null terminated string will follow: 0x84.
                // byte 10 .. n: filename of file to open, e.g. "../prjs/EV3Wifi/INBOX.rtf" where EV3Wifi is the name of the EV3 program and INBOX the name of the EV3 file.
                // byte n+1: null termination character: 0x00.
                // byte n+2: location for first return value of OPEN_READ, the file handle: global variable offset 0 encoded as 0x60.
                // byte n+3: location for second return value of OPEN_READ, the file size: global variable offset 4 encoded as 0x64.
                // byte n+4: byte code of the next command in the same byte stream: 0xC0 = opFile command.
                // byte n+5: subcommand: 0x05 = READ_TEXT.
                // byte n+6: first parameter to READ_TEXT: location of file handle: global variable offset 0 encoded as 0x60.
                // byte n+7: second parameter to READ_TEXT: delimeter code = no delimeter = 0x00.
                // byte n+8: third parameter to READ_TEXT: maximal string length to read: 0xF0 = 240 (not to exceed 256 return bytes).
                // byte n+9: location for return value of READ_TEXT, the string read: global variable offset 8 encoded as 0x68.
                // byte n+10: byte code of the next command in the same byte stream: 0xC0 = opFile command.
                // byte n+11: subcommand: 0x07 = CLOSE.
                // byte n+12: parameter to CLOSE: location of file handle = 0x60.
                copyArray(byteArray, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0xFB, 0x00, 0xC0, 0x01, 0x84 }, 0);
                copyArray(byteArray, Encoding.ASCII.GetBytes(fileName), 10);
                copyArray(byteArray, new byte[] { (byte) '\0', 0x60, 0x64, 0xC0, 0x05, 0x60, 0x00, 0xF0, 0x68, 0xC0, 0x07, 0x60 }, 10 + fileName.Length);
                byteArray[0] = (byte) (len - 2);  // length of array excluding the two length bytes
                Send(tcpSocket, byteArray);
                /*
                for (int i = 0; i < byteArray.Length; i++)
                {
                    Console.WriteLine("byteArray[{0}] = {1:X}, {2}", i, byteArray[i], (char)byteArray[i]);
                }
                */
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // Utility method to copy sub array ar2 into the main array ar1 starting at index 'pos'.
        private void copyArray(byte[] ar1, byte[] ar2, int pos)
        {
            try
            {
                for (int i = 0; i < ar2.Length; i++)
                {
                    ar1[i + pos] = ar2[i];
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
