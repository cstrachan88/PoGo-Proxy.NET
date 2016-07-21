using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Newtonsoft.Json;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;

namespace PoGo_Proxy
{
    public class ApiLogger
    {
        public Dictionary<ulong, ApiLogEntry> Log { get; } = new Dictionary<ulong, ApiLogEntry>();

        public ApiMessageBlock AddRequest(RequestEnvelope requestEnvelope, DateTime callTime)
        {
            if (Log.ContainsKey(requestEnvelope.RequestId)) throw new ArgumentException($"Request Id ({requestEnvelope.RequestId}) already exists in the Api log.");

            var messageBlock = new ApiMessageBlock
            {
                RequestId = requestEnvelope.RequestId,
                CallTime = callTime,
                ApiCallType = ApiCallType.Request,
                Messages = new Dictionary<RequestType, IMessage>()
            };

            foreach (Request request in requestEnvelope.Requests)
            {
                var type = Type.GetType("POGOProtos.Networking.Requests.Messages." + request.RequestType + "Message");

                var instance = (IMessage)Activator.CreateInstance(type);
                instance.MergeFrom(request.RequestMessage);

                messageBlock.Messages.Add(request.RequestType, instance);
            }

            var logEntry = new ApiLogEntry
            {
                Request = messageBlock
            };
            Log.Add(requestEnvelope.RequestId, logEntry);

            return messageBlock;
        }

        public ApiMessageBlock AddResponse(ResponseEnvelope responseEnvelope, DateTime callTime)
        {
            if (!Log.ContainsKey(responseEnvelope.RequestId)) throw new KeyNotFoundException($"Requests list doesn't have specified Request Id ({responseEnvelope.RequestId}).");

            var messageBlock = new ApiMessageBlock
            {
                RequestId = responseEnvelope.RequestId,
                CallTime = callTime,
                ApiCallType = ApiCallType.Response,
                Messages = new Dictionary<RequestType, IMessage>()
            };

            var logEntry = Log[responseEnvelope.RequestId];

            if (logEntry.Request.Messages.Count != responseEnvelope.Returns.Count) throw new RankException("Request messages count is different than the response messages count.");

            var requestTypes = logEntry.Request.Messages.Keys.ToList();

            for (int i = 0; i < responseEnvelope.Returns.Count; i++)
            {
                var type = Type.GetType("POGOProtos.Networking.Responses." + requestTypes[i] + "Response");

                var instance = (IMessage)Activator.CreateInstance(type);
                instance.MergeFrom(responseEnvelope.Returns[i]);

                messageBlock.Messages.Add(requestTypes[i], instance);
            }

            logEntry.Response = messageBlock;

            return messageBlock;
        }

        public override string ToString()
        {
            // Pretty prints object to json
            return JsonConvert.SerializeObject(this, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
        }
    }

    public class ApiLogEntry
    {
        public ApiMessageBlock Request { get; set; }
        public ApiMessageBlock Response { get; set; }
    }

    public class ApiMessageBlock
    {
        public ulong RequestId { get; set; }
        public DateTime CallTime { get; set; }
        public ApiCallType ApiCallType { get; set; }
        public Dictionary<RequestType, IMessage> Messages { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            switch (ApiCallType)
            {
                case ApiCallType.Request:
                    sb.AppendLine($"[-->] Request to pogo api server ({RequestId})");
                    foreach (var pair in Messages)
                    {
                        sb.AppendLine($" [+] {pair.Key}: {JsonConvert.SerializeObject(pair.Value)}");
                    }
                    break;

                case ApiCallType.Response:
                    sb.AppendLine($"[<--] Response from pogo api server ({RequestId})");
                    foreach (var pair in Messages)
                    {
                        sb.AppendLine($" [-] {pair.Key}: {JsonConvert.SerializeObject(pair.Value)}");
                    }
                    break;
            }
            sb.AppendLine();
            return sb.ToString();
        }
    }

    public enum ApiCallType
    {
        Request,
        Response
    }
}
