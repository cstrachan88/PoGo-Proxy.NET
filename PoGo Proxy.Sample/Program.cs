using System;
using System.IO;

namespace PoGo_Proxy.Sample
{
    class Program
    {
        private static readonly ProxyController Controller = new ProxyController();

        static void Main(string[] args)
        {
            Console.WriteLine("Hit any key to exit..");
            Console.WriteLine();

            Controller.Start();

            Console.Read();

            Controller.Stop();

            var path = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yyyy-MM-dd-hh-mm") + "-log.json");
            File.WriteAllText(path, Controller.ApiLogger.ToString());
        }
    }
}
