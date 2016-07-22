using System;
using System.Collections.Generic;
using Google.Protobuf;
using Newtonsoft.Json;
using POGOProtos.Networking.Requests;

namespace PoGo_Proxy
{
    public class MessageBlock
    {
        public DateTime MessageInitialized { get; set; }
        public Dictionary<RequestType, IMessage> ParsedMessages { get; set; }

        public override string ToString()
        {
            return "  Parsed Messages:\n" + JsonConvert.SerializeObject(ParsedMessages);
        }
    }
}
