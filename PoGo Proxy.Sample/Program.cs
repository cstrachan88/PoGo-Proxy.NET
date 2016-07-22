using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Responses;

namespace PoGo_Proxy.Sample
{
    class Program
    {
        private static List<RequestHandledEventArgs> _apiLog;

        static void Main()
        {
            _apiLog = new List<RequestHandledEventArgs>();

            Console.WriteLine("Hit any key to stop proxy..");
            Console.WriteLine();

            var controller = new ProxyController("192.168.0.19", 61212)
            {
                Out = Console.Out
            };
            controller.RequestHandled += Controller_ResponseReceived;

            controller.Start();

            Console.ReadKey();

            controller.Stop();

            Console.WriteLine("Hit any key to save log of requests and responses and exit..");
            Console.ReadKey();

            // Log requests and responses
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yy-MM-dd-hh-mm") + "-log.json"),
                JsonConvert.SerializeObject(_apiLog, Formatting.Indented, new StringEnumConverter()));
        }

        private static void Controller_ResponseReceived(object sender, RequestHandledEventArgs e)
        {
            // Update log
            _apiLog.Add(e);

            foreach (var responsePair in e.ResponseBlock.ParsedMessages)
            {
                if (responsePair.Key == RequestType.GetInventory)
                {
                    var inventory = responsePair.Value as GetInventoryResponse;

                    if (inventory != null && inventory.Success)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(inventory.InventoryDelta));
                    }
                }
            }
        }
    }
}
