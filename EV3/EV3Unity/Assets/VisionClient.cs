using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

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
	public StringBuilder sb = new StringBuilder ();
}

public class VisionClient
{
	public bool isConnected = false;
	// The port number for the remote device.
	private const int port = 5000;
	// ManualResetEvent instances signal completion.
	private static ManualResetEvent connectDone = 
		new ManualResetEvent (false);
	private static ManualResetEvent sendDone = 
		new ManualResetEvent (false);
	private static ManualResetEvent receiveDone = 
		new ManualResetEvent (false);
	private Socket tcpSocket;

	// The response from the remote device.
	private string response = String.Empty;

	public bool Connect ()
	{  
		// Connect to a remote device.  
		try {  
			// Establish the remote endpoint for the socket.  
			IPAddress ipAddress = IPAddress.Parse ("127.0.0.1");
			IPEndPoint remoteEP = new IPEndPoint (ipAddress, port);  

			// Create a TCP/IP socket.  
			tcpSocket = new Socket (ipAddress.AddressFamily,  
				SocketType.Stream, ProtocolType.Tcp);  

			// Connect to the remote endpoint.  
			tcpSocket.BeginConnect (remoteEP,   
				new AsyncCallback (ConnectCallback), tcpSocket);  
			if (connectDone.WaitOne (5000)) {
				isConnected = true;
			}
			return isConnected;
		} catch (Exception e) {  
			Debug.Log (e.ToString ());
			return false;
		}  
	}

	public void Disconnect ()
	{
		try {
			// Release the sockets and reset the connectDone flag.
			isConnected = false;
			// Wait for a short while before closing the socket so callback methods can finish.
			Thread.Sleep (100);
			if (tcpSocket != null && tcpSocket.Connected == true) {
				tcpSocket.Shutdown (SocketShutdown.Both);
				tcpSocket.Close ();
			}
			connectDone.Reset ();
			sendDone.Reset ();
			receiveDone.Reset ();
		} catch (Exception e) {
			Debug.Log (e.ToString ());
		}
	}

	public void SendMessage (string msg)
	{
		// Send test data to the remote device.  
		Send (tcpSocket, msg);  
		sendDone.WaitOne (5000);  
	}

	public string ReceiveMessage ()
	{
		// Receive the response from the remote device.  
		Receive (tcpSocket);
		receiveDone.WaitOne (5000);  
		return response;
	}

	private static void ConnectCallback (IAsyncResult ar)
	{  
		try {  
			// Retrieve the socket from the state object.  
			Socket client = (Socket)ar.AsyncState;  

			// Complete the connection.  
			client.EndConnect (ar);  

			//Debug.Log ("Socket connected to: " + client.RemoteEndPoint.ToString ());  

			// Signal that the connection has been made.  
			connectDone.Set ();  
		} catch (Exception e) {  
			Debug.Log (e.ToString ()); 
		}  
	}

	private void Receive (Socket client)
	{  
		try {  
			// Create the state object.  
			StateObject state = new StateObject ();  
			state.workSocket = client;  

			// Begin receiving the data from the remote device.  
			client.BeginReceive (state.buffer, 0, StateObject.BufferSize, 0,  
				new AsyncCallback (ReceiveCallback), state);  
		} catch (Exception e) {  
			Debug.Log (e.ToString ());  
		}  
	}

	private void ReceiveCallback (IAsyncResult ar)
	{  
		if (isConnected) {
			try {  
				// Retrieve the state object and the client socket   
				// from the asynchronous state object.  
				StateObject state = (StateObject)ar.AsyncState;  
				Socket client = state.workSocket;

				// Read data from the remote device.  
				int bytesRead = client.EndReceive (ar);

				if (bytesRead > 0) {  
					// There might be more data, so store the data received so far.  
					state.sb.Append (Encoding.ASCII.GetString (state.buffer, 0, bytesRead));
					if (state.sb.Length > 1) {
						response = state.sb.ToString ();
						// Signal that all bytes have been received.  
						receiveDone.Set ();
					}
				}
			} catch (Exception e) {  
				Debug.Log (e.ToString ()); 
			}
		}
	}


	private void Send (Socket client, String data)
	{  
		// Convert the string data to byte data using ASCII encoding.  
		byte[] byteData = Encoding.ASCII.GetBytes (data);  

		// Begin sending the data to the remote device.  
		client.BeginSend (byteData, 0, byteData.Length, 0,  
			new AsyncCallback (SendCallback), client);  
	}

	private void SendCallback (IAsyncResult ar)
	{  
		try {  
			// Retrieve the socket from the state object.  
			Socket client = (Socket)ar.AsyncState;  

			// Complete sending the data to the remote device.  
			int bytesSent = client.EndSend (ar);  

			// Signal that all bytes have been sent.  
			sendDone.Set ();  
		} catch (Exception e) {  
			Debug.Log (e.ToString ());  
		}  
	}
}