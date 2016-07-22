using System;
using System.Collections.Generic;
using Google.Protobuf;
using POGOProtos.Networking.Requests;

namespace PoGo_Proxy
{
    public class MessageBlock
    {
        public DateTime MessageInitialized { get; set; }
        public Dictionary<RequestType, IMessage> ParsedMessages { get; set; }
    }
}
