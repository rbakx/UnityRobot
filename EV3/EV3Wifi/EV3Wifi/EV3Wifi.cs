using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace EV3WifiLib
{
    // State object for receiving data from remote device.
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

    public class EV3Wifi
    {
        private UdpClient udpSocket;
        private IPEndPoint source;
        public IPEndPoint target;
        public String serialNumber;
        public Socket tcpSocket;
        // Array to keep the message which is filled by the asynchronous callback method.
        public byte[] message = new byte[1024];

        // ManualResetEvent instances signal completion.
        private ManualResetEvent connectDone = new ManualResetEvent(false);
        private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);

        // The response from the remote device.
        private String response = String.Empty;

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

        public Boolean Connect()
        {
            try
            {
                Boolean ok = NegotiateUdp();
                if (ok != true)
                {
                    return false;
                }
                return (StartTCPClient());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        private Boolean NegotiateUdp()
        {
            // Creates a UdpClient for reading incoming data.
            // With no port number specified the UdpClient will automatically pick an available port number as the source port.
            try
            {
                udpSocket = new UdpClient(3015);
                // Schedule the first receive operation.
                udpSocket.BeginReceive(new AsyncCallback(OnUdpData), udpSocket);

                Boolean UdpConfirmed = false;
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
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        private Boolean StartTCPClient()
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
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public void StopTCPClient()
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

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket .
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device. 
                int bytesRead = client.EndReceive(ar);


                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // Continue receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                // Put data it in response.
                response = state.sb.ToString();
                receiveDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Send(Socket client, String data)
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

        public void Send(Socket client, byte[] byteData)
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

        public void SendMessage(String msg, String mbox)
        {
            try
            {
                byte[] byteArray;
                int len = 11 + msg.Length + mbox.Length;
                byteArray = new byte[len];
                copyArray(byteArray, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x81, 0x9E, 0x00 }, 0);
                copyArray(byteArray, Encoding.ASCII.GetBytes(mbox), 7);
                copyArray(byteArray, new byte[] { (byte)'\0' }, 7 + mbox.Length);
                copyArray(byteArray, Encoding.ASCII.GetBytes(msg), 7 + mbox.Length + 3);
                copyArray(byteArray, new byte[] { (byte)'\0' }, 7 + mbox.Length + 3 + msg.Length);
                byteArray[0] = (byte)(len - 2);  // length of array excluding the two length bytes
                byteArray[6] = (byte)(mbox.Length + 1);  // length of mailbox name
                byteArray[7 + mbox.Length + 1] = (byte)(msg.Length + 1);  // length of message
                Send(tcpSocket, byteArray);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void startReceiveMessage(String project, String mbox)
        {
            try
            {
                String fileName = "../prjs/" + project + "/" + mbox + ".rtf";
                byte[] byteArray;
                int len = 21 + fileName.Length;

                byteArray = new byte[len];
                copyArray(byteArray, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0xC0, 0x01, 0x84 }, 0);
                copyArray(byteArray, Encoding.ASCII.GetBytes(fileName), 10);
                copyArray(byteArray, new byte[] { (byte)'\0', 0x60, 0x64, 0xC0, 0x03, 0x60, 0x00, 0x68, 0xC0, 0x07, 0x60 }, 10 + fileName.Length);
                byteArray[0] = (byte)(len - 2);  // length of array excluding the two length bytes
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

        public String ReceiveMessage(String project, String mbox)
        {
            try
            {
                // Retrieve the response string.
                String tmpResponse = response;
                response = "";  // clear response to indicate it is handled.
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
