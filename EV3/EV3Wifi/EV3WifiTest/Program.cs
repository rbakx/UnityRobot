using System;
using EV3WifiLib;

namespace EV3WifiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the EV3 Wifi communication example!");
            EV3Wifi myEV3 = new EV3Wifi();

            myEV3.Connect();
            Console.WriteLine("Connected to {0}, serialnumber {1}", myEV3.target.ToString(), myEV3.serialNumber);
            Console.WriteLine("Press any key to continue");
            Console.ReadLine();

            myEV3.SendMessage("beep", "INBOX");
            // Calling ReceiveMessage is non -blocking. It will retrieve the previous message and initiate a new message retrieval.
            String response = myEV3.ReceiveMessage("EV3Wifi", "OUTBOX");
            Console.WriteLine("String sent and response received : {0}", response);
            Console.WriteLine("Press any key to continue");
            Console.ReadLine();
            // ReceiveMessage will now return the response (if any) to the SendMessage.
            response = myEV3.ReceiveMessage("EV3Wifi", "OUTBOX");
            Console.WriteLine("Response received : {0}", response);
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
            myEV3.StopTCPClient();
        }
    }
}
