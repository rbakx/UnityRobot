using System;
using EV3WifiLib;

namespace EV3WifiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the EV3 communication example!");
            EV3Wifi myEV3 = new EV3Wifi();
            myEV3.Connect();
            Console.WriteLine("Connected, press any key to continue");
            Console.ReadLine();
            byte[] byteArray = new byte[] { 0x10, 0x00, 0x00, 0x00, 0x81, 0x9E, 0x03, (byte)'A', (byte)'B', (byte)'\0', 0x05, 0x00, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)'\0' };
            myEV3.Send(myEV3.tcpSocket, byteArray);

            Console.WriteLine("String sent, press any key to continue");
            Console.ReadLine();
            String response = myEV3.Receive();
            Console.WriteLine("Response received : {0}", response);
            Console.WriteLine("Connected to {0}, serialnumber {1}", myEV3.target.ToString(), myEV3.serialNumber);
            Console.WriteLine("press any key to exit");
            Console.ReadLine();
            myEV3.StopTCPClient();
        }
    }
}
