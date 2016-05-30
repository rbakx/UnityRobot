using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Text.RegularExpressions;


namespace UnityEV3
{
    class Program
    {
        static void Main(string[] args)
        {
            String serialNumber;
            Console.WriteLine("Welcome to the EV3 communication example!");
            EV3 myEV3 = new EV3();
            myEV3.ConnectToEv3();
            myEV3.StartTCPClient();
        }
    }

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

        class EV3
    {
        private UdpClient socket;
        private IPEndPoint source;
        private IPEndPoint target;
        private String serialNumber;
        // static array to keep the message which is filled by the asynchronous callback method.
        public static byte[] message = new byte[1024];

        // The port number for the remote device.
        private const int port = 11000;

        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.
        private static String response = String.Empty;


        private void OnUdpData(IAsyncResult result)
        {
            // This is what had been passed into BeginReceive as the second parameter.
            UdpClient socket = result.AsyncState as UdpClient;
            // Get the actual message and fill out the source.
            message = socket.EndReceive(result, ref source);
            // Schedule the next receive operation once reading is done.
            socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
        }

        public void ConnectToEv3()
        {
            // Creates a UdpClient for reading incoming data.
            // With no port number specified the UdpClient will automatically pick an available port number as the source port.
            socket = new UdpClient(3015);
            // Schedule the first receive operation.
            socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
            target = new IPEndPoint(IPAddress.Parse("192.168.1.42"), 12345);

            Boolean UdpConfirmed = false;
            while (UdpConfirmed == false)
            {
                String msgStr = Encoding.ASCII.GetString(message, 0, message.Length);
                Regex regex = new Regex("Serial-Number: (.*)");
                Match match = regex.Match(msgStr);
                if (match.Success)
                {
                    serialNumber = match.Groups[1].Value;
                    Console.WriteLine("match: " + serialNumber + "\n");
                    Console.WriteLine("going to send hi to: " + source + "\n");
                    byte[] msg = Encoding.ASCII.GetBytes("hi");
                    socket.Send(msg, msg.Length, source);
                    UdpConfirmed = true;
                }
                else
                {
                    Console.WriteLine("no match: " + "\n");
                }
            }

        }

        public void StartTCPClient()
        {
            // Connect to a remote device.
            try
            {
                // Create a TCP/IP socket.
                Socket client = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.

                IPEndPoint remoteEP = new IPEndPoint(source.Address, 5555);
                client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                // Send test data to the remote device.
                String str = "GET /target?sn=" + serialNumber + " VMTP1.0\nProtocol: EV3";
                Send(client, str);
                sendDone.WaitOne();

                // Receive the response from the remote device.
                Receive(client);
                receiveDone.WaitOne();

                // Write the response to the console.
                Console.WriteLine("Response received : {0}", response);
                Console.Write("press any key to exit");
                Console.ReadLine();

                // Release the socket.
                client.Shutdown(SocketShutdown.Both);
                client.Close();

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

                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Receive(Socket client)
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
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Continue receiving the data from the remote device.
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                    // Put data it in response.
                    response = state.sb.ToString();
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

    }
}
