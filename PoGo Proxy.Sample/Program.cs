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

            var controller = new ProxyController("192.168.0.19", 61212) {Out = Console.Out};
            
            controller.RequestHandled += Controller_RequestHandled;

            controller.Start();
            Console.ReadKey();
            controller.Stop();

            Console.WriteLine("Hit any key to save log of requests and responses and exit..");
            Console.ReadKey();

            // Log requests and responses
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yy-MM-dd-hh-mm") + "-log.json"),
                JsonConvert.SerializeObject(_apiLog, Formatting.Indented, new StringEnumConverter()));
        }

        private static void Controller_RequestHandled(object sender, RequestHandledEventArgs e)
        {
            // Update log
            _apiLog.Add(e);

            // Parse responses
            Console.WriteLine("Response blocks:");
            foreach (var responsePair in e.ResponseBlock.ParsedMessages)
            {
                Console.WriteLine("  " + responsePair.Key);

                switch (responsePair.Key)
                {
                    case RequestType.Encounter:
                        Console.WriteLine("    Name : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.PokemonId);
                        Console.WriteLine("    Attack : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.IndividualAttack);
                        Console.WriteLine("    Defense : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.IndividualDefense);
                        Console.WriteLine("    Stamina : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.IndividualStamina);
                        break;

                    case RequestType.DiskEncounter:
                        Console.WriteLine("    Name : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.PokemonId);
                        Console.WriteLine("    Attack : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.IndividualAttack);
                        Console.WriteLine("    Defense : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.IndividualDefense);
                        Console.WriteLine("    Stamina : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.IndividualStamina);
                        break;
                }
            }
            Console.WriteLine();
        }
    }
}
