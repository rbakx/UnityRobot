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
            myEV3.SendMessage("beep", "AB");
            String message = myEV3.ReceiveMessage();
            Console.WriteLine("Message received : {0}", message);

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
