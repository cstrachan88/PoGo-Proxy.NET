using System;
using System.IO;
using System.Linq;

namespace PoGo_Proxy.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hit any key to exit..");
            Console.WriteLine();

            var controller = new ProxyController("192.168.0.19", 61212)
            {
                Out = Console.Out
            };
            controller.ResponseReceived += Controller_ResponseReceived;

            controller.Start();

            // If the user presses a key, exit
            Console.Read();

            controller.Stop();
        }

        private static void Controller_ResponseReceived(object sender, ResponseEventArgs e)
        {
            Console.WriteLine("[<-] Received responses for request id " + e.RequestId);

            foreach (var response in e.Responses.ParsedMessages)
            {
                Console.WriteLine($" [-] Received {response.Key}");
            }
            Console.WriteLine();
        }
    }
}
