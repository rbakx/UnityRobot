using System;
using System.Threading;
using EV3WifiLib;

namespace EV3WifiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the EV3 Wifi communication example!");
            EV3Wifi myEV3 = new EV3Wifi();

            String status = myEV3.Connect();
            Console.WriteLine("Connection status: " + status);
            Console.WriteLine("Connected to {0}, serialnumber {1}", myEV3.target.ToString(), myEV3.serialNumber);
            Console.WriteLine("Press any key to continue");
            Console.ReadLine();

            while (!Console.KeyAvailable) {
                myEV3.SendMessage("get_distance", "STATUS");
                // Calling ReceiveMessage is non -blocking. It will retrieve the previous message and initiate a new message retrieval.
                String strDistance = myEV3.ReceiveMessage("EV3Wifi", "DISTANCE"); float distance;
                Console.WriteLine("Response received : {0}", strDistance);
                if (float.TryParse(strDistance, out distance))
                {
                    if (distance > 53.0)
                    {
                        myEV3.SendMessage("forward", "MOVE");
                    }
                    else if (distance < 47.0)
                    {
                        myEV3.SendMessage("backward", "MOVE");
                    }
                }
                Thread.Sleep(100);
            }
            myEV3.Disconnect(); 
        }
    }
}
