using System;
using System.IO;

namespace PoGo_Proxy.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hit any key to exit..");
            Console.WriteLine();

            var controller = new ProxyController("192.168.0.19", 8081)
            {
                Out = Console.Out
            };

            controller.Start();

            // If the user presses a key, exit
            Console.Read();

            controller.Stop();

            var path = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yyyy-MM-dd-hh-mm") + "-log.json");
            File.WriteAllText(path, controller.ApiLogger.ToString());
        }
    }
}
