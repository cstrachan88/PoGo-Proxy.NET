using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using POGOProtos.Map;
using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Responses;

namespace PoGo_Proxy.Sample
{
    internal class Program
    {
        private static List<RequestHandledEventArgs> _apiLog;
        private static List<Tuple<RequestType, string>> _outputMessages;
        private static Dictionary<ulong, NearbyPokemon> _nearbyPokemon;

        private static void Main()
        {
            _apiLog = new List<RequestHandledEventArgs>();
            _outputMessages = new List<Tuple<RequestType, string>>();
            _nearbyPokemon = new Dictionary<ulong, NearbyPokemon>();

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

            // Print response to console only if new
            foreach (var responsePair in e.ResponseBlock.ParsedMessages)
            {
                switch (responsePair.Key)
                {
                    case RequestType.GetMapObjects:
                        ParseMapObject((GetMapObjectsResponse) responsePair.Value);
                        continue;
                    case RequestType.DownloadSettings:
                    case RequestType.GetAssetDigest:
                    case RequestType.DownloadRemoteConfigVersion:
                        continue;
                }

                var output = _outputMessages.SingleOrDefault(tuple => tuple.Item1 == responsePair.Key);

                var outputMessage = ParseResponseMessage(responsePair);

                if (output == null)
                {
                    _outputMessages.Add(new Tuple<RequestType, string>(responsePair.Key, outputMessage));

                    Console.WriteLine($"[+] New {responsePair.Key} response");
                    Console.WriteLine(outputMessage);
                }
                else
                {
                    if (output.Item2 == outputMessage) continue;

                    Console.WriteLine($"[+] Updated {responsePair.Key} response");
                    Console.WriteLine(outputMessage);
                }
            }
        }

        private static void ParseMapObject(GetMapObjectsResponse response)
        {
            if (response.Status == MapObjectsStatus.Success)
            {
                foreach (var cell in response.MapCells)
                {
                    foreach (var poke in cell.NearbyPokemons)
                    {
                        _nearbyPokemon[poke.EncounterId] = poke;
                    }
                }
            }

            if (_nearbyPokemon.Count > 0) Console.WriteLine("NEARBY POKEMON");
            foreach (var poke in _nearbyPokemon.Values)
            {
                Console.WriteLine($"  Name: {poke.PokemonId}, Distance: {poke.DistanceInMeters}m (This value will always be 200m until Niantic fixes servers)");
            }
        }

        private static string ParseResponseMessage(KeyValuePair<RequestType, IMessage> responsePair)
        {
            var sb = new StringBuilder();

            switch (responsePair.Key)
            {
                default:
                    sb.AppendLine("    Ignored");
                    break;

                case RequestType.GetPlayer:
                {
                    var response = (GetPlayerResponse) responsePair.Value;
                    if (response.Success)
                    {
                        sb.AppendLine($"    Name: {response.PlayerData.Username}");
                        foreach (var currencyPair in response.PlayerData.Currencies)
                        {
                            sb.AppendLine($"    {currencyPair.Name}: {currencyPair.Amount}");
                        }
                    }
                    break;
                }
                case RequestType.GetHatchedEggs:
                {
                    var response = (GetHatchedEggsResponse) responsePair.Value;
                    if (response.Success)
                    {
                        if (response.PokemonId.Count == 0) sb.AppendLine("    No hatched eggs");
                        for (int i = 0; i < response.PokemonId.Count; i++)
                        {
                            sb.AppendLine($"    Egg {i}: ");
                            sb.AppendLine("      PokemonId: " + response.PokemonId[i]);
                            sb.AppendLine("      ExperienceAwarded: " + response.ExperienceAwarded[i]);
                            sb.AppendLine("      CandyAwarded: " + response.CandyAwarded[i]);
                            sb.AppendLine("      StardustAwarded: " + response.StardustAwarded[i]);
                        }
                    }
                    break;
                }
                case RequestType.GetInventory:
                {
                    var response = (GetInventoryResponse) responsePair.Value;
                    if (response.Success)
                    {
                        sb.AppendLine("    lots of data to parse");
                    }
                    break;
                }
                case RequestType.CheckAwardedBadges: // TODO should this be GetAwardedBadges in POGOProtocs?
                {
                    var response = (CheckAwardedBadgesResponse) responsePair.Value;
                    if (response.Success)
                        {
                            if (response.AwardedBadges.Count == 0) sb.AppendLine("    No awarded badges");
                            for (int i = 0; i < response.AwardedBadges.Count; i++)
                        {
                            sb.AppendLine($"    Badge {i}: ");
                            sb.AppendLine("      AwardedBadges: " + response.AwardedBadges[i]);
                            sb.AppendLine("      AwardedBadgeLevels: " + response.AwardedBadgeLevels[i]);
                        }
                    }
                    break;
                }
                case RequestType.GetGymDetails:
                {
                    var response = (GetGymDetailsResponse) responsePair.Value;

                    if (response.Result == GetGymDetailsResponse.Types.Result.Success)
                    {
                        sb.AppendLine($"    Name: {response.Name}");
                        sb.AppendLine($"    Id: {response.GymState.FortData.Id}");
                    }
                    break;
                }
                case RequestType.Encounter:
                {
                    var response = (EncounterResponse) responsePair.Value;

                    sb.AppendLine("    PokemonId: " + response.WildPokemon.PokemonData.PokemonId);
                    sb.AppendLine("    IndividualAttack: " + response.WildPokemon.PokemonData.IndividualAttack);
                    sb.AppendLine("    IndividualDefense: " + response.WildPokemon.PokemonData.IndividualDefense);
                    sb.AppendLine("    IndividualStamina: " + response.WildPokemon.PokemonData.IndividualStamina);
                    break;
                }
                case RequestType.DiskEncounter:
                {
                    var response = (DiskEncounterResponse) responsePair.Value;

                    sb.AppendLine("    PokemonId: " + response.PokemonData.PokemonId);
                    sb.AppendLine("    IndividualAttack: " + response.PokemonData.IndividualAttack);
                    sb.AppendLine("    IndividualDefense: " + response.PokemonData.IndividualDefense);
                    sb.AppendLine("    IndividualStamina: " + response.PokemonData.IndividualStamina);
                    break;
                }
            }
            return sb.ToString();
        }
    }
}