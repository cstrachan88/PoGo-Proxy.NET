using System;
using Newtonsoft.Json;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Responses;

namespace PoGo_Proxy.Sample
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Hit any key to exit..");
            Console.WriteLine();

            var controller = new ProxyController("192.168.0.19", 61212)
            {
                Out = Console.Out
            };
            controller.ResponseReceived += Controller_ResponseReceived;

            controller.Start();

            // Press a key to stop proxy, then press a key to exit
            Console.ReadKey();

            controller.Stop();

            Console.ReadKey();
        }

        private static void Controller_ResponseReceived(object sender, ResponseEventArgs e)
        {
            //Console.WriteLine("[<-] Received responses for request id " + e.RequestId);

            //foreach (var response in e.Responses.ParsedMessages)
            //{
            //    Console.WriteLine($" [-] Received {response.Key}");
            //}
            //Console.WriteLine();

            foreach (var responsePair in e.Responses.ParsedMessages)
            {
                if (responsePair.Key == RequestType.GetInventory)
                {
                    var inventory = responsePair.Value as GetInventoryResponse;

                    if (inventory != null && inventory.Success)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(inventory.InventoryDelta));
                    }

                    //Console.WriteLine("Inventory:");
                    //Console.WriteLine("  " + (inventory.OriginalTimestampMs == 0 ? "First update" : DateHelpers.FromUnixTime(inventory.OriginalTimestampMs).ToString("g")));
                    //Console.WriteLine("  " + DateHelpers.FromUnixTime(inventory.NewTimestampMs).ToString("g"));

                    //foreach (var inventoryItem in inventory.InventoryItems)
                    //{
                    //    if (inventoryItem.InventoryItemData.PokemonData != null)
                    //    {
                    //        Console.WriteLine("    PokemonData: " + inventoryItem.InventoryItemData.PokemonData.PokemonId);
                    //    }
                    //    if (inventoryItem.InventoryItemData.Item != null)
                    //    {
                    //        Console.WriteLine("    Item: " + inventoryItem.InventoryItemData.Item.ItemId);
                    //    }
                    //    if (inventoryItem.InventoryItemData.PokedexEntry != null)
                    //    {
                    //        Console.WriteLine("    PokedexEntry: " + inventoryItem.InventoryItemData.PokedexEntry.PokemonId);
                    //    }
                    //    if (inventoryItem.InventoryItemData.PlayerStats != null)
                    //    {
                    //        Console.WriteLine("    PlayerStats: " + inventoryItem.InventoryItemData.PlayerStats.Experience);
                    //    }
                    //    if (inventoryItem.InventoryItemData.PlayerCurrency != null)
                    //    {
                    //        Console.WriteLine("    PlayerCurrency: " + inventoryItem.InventoryItemData.PlayerCurrency.Gems);
                    //    }
                    //    if (inventoryItem.InventoryItemData.PlayerCamera != null)
                    //    {
                    //        Console.WriteLine("    PlayerCamera: " + inventoryItem.InventoryItemData.PlayerCamera.IsDefaultCamera);
                    //    }
                    //    if (inventoryItem.InventoryItemData.InventoryUpgrades != null)
                    //    {
                    //        Console.WriteLine("    InventoryUpgrades: " + inventoryItem.InventoryItemData.InventoryUpgrades.InventoryUpgrades_[0].UpgradeType);
                    //    }
                    //    if (inventoryItem.InventoryItemData.AppliedItems != null)
                    //    {
                    //        Console.WriteLine("    AppliedItems: " + inventoryItem.InventoryItemData.AppliedItems.Item[0].ItemId);
                    //    }
                    //    if (inventoryItem.InventoryItemData.EggIncubators != null)
                    //    {
                    //        Console.WriteLine("    EggIncubators: " + inventoryItem.InventoryItemData.EggIncubators.EggIncubator.IncubatorType);
                    //    }
                    //    if (inventoryItem.InventoryItemData.PokemonFamily != null)
                    //    {
                    //        Console.WriteLine("    PokemonFamily: " + inventoryItem.InventoryItemData.PokemonFamily.Candy);
                    //    }
                    
                }
            }
        }
    }
}
