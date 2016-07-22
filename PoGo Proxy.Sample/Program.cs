using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

            // Using stringbuilder to group response blocks since messages can get overlayed due to threading
            var sb = new StringBuilder();

            // Parse responses
            sb.AppendLine("[<-] Response blocks:");
            foreach (var responsePair in e.ResponseBlock.ParsedMessages)
            {
                sb.AppendLine(" [+] " + responsePair.Key);

                switch (responsePair.Key)
                {
                    case RequestType.Encounter:
                        sb.AppendLine("     Name : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.PokemonId);
                        sb.AppendLine("     Attack : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.IndividualAttack);
                        sb.AppendLine("     Defense : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.IndividualDefense);
                        sb.AppendLine("     Stamina : " + ((EncounterResponse)responsePair.Value).WildPokemon.PokemonData.IndividualStamina);
                        break;

                    case RequestType.DiskEncounter:
                        sb.AppendLine("     Name : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.PokemonId);
                        sb.AppendLine("     Attack : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.IndividualAttack);
                        sb.AppendLine("     Defense : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.IndividualDefense);
                        sb.AppendLine("     Stamina : " + ((DiskEncounterResponse)responsePair.Value).PokemonData.IndividualStamina);
                        break;
                }
            }
            Console.WriteLine(sb.ToString());
        }
    }
}
