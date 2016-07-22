using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Newtonsoft.Json;
using POGOProtos.Networking.Requests;

namespace PoGo_Proxy
{
    public class MessageBlock
    {
        public ulong RequestId { get; set; }
        public DateTime MessageInitialized { get; set; }
        public MessageBlockType MessageBlockType { get; set; }
        public Dictionary<RequestType, IMessage> ParsedMessages { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            switch (MessageBlockType)
            {
                case MessageBlockType.Request:
                    sb.AppendLine($"  Request to pogo api server ({RequestId})");
                    foreach (var pair in ParsedMessages)
                    {
                        sb.AppendLine($"    {pair.Key}: {JsonConvert.SerializeObject(pair.Value)}");
                    }
                    break;

                case MessageBlockType.Response:
                    sb.AppendLine($"  Response from pogo api server ({RequestId})");
                    foreach (var pair in ParsedMessages)
                    {
                        sb.AppendLine($"    {pair.Key}: {JsonConvert.SerializeObject(pair.Value)}");
                    }
                    break;
            }
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
