using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
using Newtonsoft.Json;
using POGOProtos.Networking.Envelopes;
using POGOProtos.Networking.Requests;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace PoGo_Proxy
{
    public sealed class ProxyController
    {
        private readonly ProxyServer _proxyServer;
        private readonly Dictionary<ulong, RequestHandledEventArgs> _apiBlocks;

        public event EventHandler<RequestHandledEventArgs> RequestHandled;

        public string Ip { get; }
        public int Port { get; }
        public TextWriter Out { get; set; }

        public ProxyController(string ipAddress, int port)
        {
            _proxyServer = new ProxyServer();
            _apiBlocks = new Dictionary<ulong, RequestHandledEventArgs>();

            Ip = ipAddress;
            Port = port;
        }

        public void Start()
        {
            // Link up handlers
            _proxyServer.BeforeRequest += OnRequest;
            _proxyServer.BeforeResponse += OnResponse;
            _proxyServer.ServerCertificateValidationCallback += OnCertificateValidation;
            _proxyServer.ClientCertificateSelectionCallback += OnCertificateSelection;

            // Set ip and port to monitor
            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Parse(Ip), Port, true);
            _proxyServer.AddEndPoint(explicitEndPoint);

            // Start proxy server
            _proxyServer.Start();

            if (Out != StreamWriter.Null)
            {
                Out.WriteLine($"[+++] Proxy started: listening at {explicitEndPoint.IpAddress}:{explicitEndPoint.Port} ");
                Out.WriteLine();
            }
        }

        public void Stop()
        {
            // Unlink handlers
            _proxyServer.BeforeRequest -= OnRequest;
            _proxyServer.BeforeResponse -= OnResponse;
            _proxyServer.ServerCertificateValidationCallback -= OnCertificateValidation;
            _proxyServer.ClientCertificateSelectionCallback -= OnCertificateSelection;

            // Stop server
            _proxyServer.Stop();

            if (Out != StreamWriter.Null) Out.WriteLine("[---] Proxy stopped");
        }

        private async Task OnRequest(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.RequestUri.Host != "pgorelease.nianticlabs.com") return;

            // Get session data
            var callTime = DateTime.Now;
            byte[] bodyBytes = await e.GetRequestBody();
            var codedInputStream = new CodedInputStream(bodyBytes);
            var requestEnvelope = RequestEnvelope.Parser.ParseFrom(codedInputStream);

            // Initialize the request block
            var requestBlock = new MessageBlock
            {
                MessageInitialized = callTime,
                ParsedMessages = new Dictionary<RequestType, IMessage>()
            };

            // Parse all the requests
            foreach (Request request in requestEnvelope.Requests)
            {
                // Had to add assembly name to end of typeName string since protocs cs files are in a different assembly
                var type = Type.GetType("POGOProtos.Networking.Requests.Messages." + request.RequestType + "Message,PoGo Proxy.Protocs");

                if (type == null)
                {
                    if (Out != StreamWriter.Null) Out.WriteLine("[*] GetType returns null for requestType: " + request.RequestType);
                    if (Out != StreamWriter.Null) Out.WriteLine("[*] Check if POGOProtos.Networking.Requests.Messages." + request.RequestType + "Message exists.");
                    if (Out != StreamWriter.Null) Out.WriteLine("[*]\n");

                    requestBlock.ParsedMessages.Add(request.RequestType, default(IMessage));
                }
                else
                {
                    var instance = (IMessage) Activator.CreateInstance(type);
                    instance.MergeFrom(request.RequestMessage);

                    requestBlock.ParsedMessages.Add(request.RequestType, instance);
                }
            }

            // TODO check if there is a double response or if a response already exists
            if (_apiBlocks.ContainsKey(requestEnvelope.RequestId))
            {
                if (Out != StreamWriter.Null)
                {
                    Out.WriteLine($"[*] Request Id({requestEnvelope.RequestId}) already exists.");
                    Out.WriteLine($"[*] Old request:\n{_apiBlocks[requestEnvelope.RequestId].RequestBlock}");
                    Out.WriteLine($"[*] New request:\n{requestBlock}");
                }

                if (_apiBlocks[requestEnvelope.RequestId].ResponseBlock == null)
                {
                    if (Out != StreamWriter.Null) Out.WriteLine($"[*] Response for the old request doesn't - replacing old request");
                    _apiBlocks[requestEnvelope.RequestId].RequestBlock = requestBlock;
                }
                else
                {
                    if (Out != StreamWriter.Null) Out.WriteLine($"[*] Response for the old request exists - do nothing");
                }
                Out.WriteLine("[*]\n");
                return;
            }

            // Initialize a new request/response paired block and track it to update response
            var args = new RequestHandledEventArgs
            {
                RequestId = requestEnvelope.RequestId,
                RequestBlock = requestBlock
            };
            _apiBlocks.Add(args.RequestId, args);
        }

        private async Task OnResponse(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.RequestUri.Host != "pgorelease.nianticlabs.com") return;

            if (e.WebSession.Response.ResponseStatusCode == "200")
            {
                // Get session data
                var callTime = DateTime.Now;
                byte[] bodyBytes = await e.GetResponseBody();
                var codedInputStream = new CodedInputStream(bodyBytes);
                var responseEnvelope = ResponseEnvelope.Parser.ParseFrom(codedInputStream);

                // Initialize the response block
                var responseBlock = new MessageBlock
                {
                    MessageInitialized = callTime,
                    ParsedMessages = new Dictionary<RequestType, IMessage>()
                };

                // Grab the paired request
                var args = _apiBlocks[responseEnvelope.RequestId];

                // Grab request types
                var requestTypes = args.RequestBlock.ParsedMessages.Keys.ToList();

                // The case of missmatched requests and responses seems to be a handshake. The inital request sends 5 messages and gets back 2 that are empty bytestrings
                if (args.RequestBlock.ParsedMessages.Count != responseEnvelope.Returns.Count)
                {
                    if ((args.RequestBlock.ParsedMessages.Count == 5 && responseEnvelope.Returns.Count == 2) &&
                        (responseEnvelope.Returns[0].IsEmpty && responseEnvelope.Returns[1].IsEmpty))
                    {
                        if (Out != StreamWriter.Null) Out.WriteLine($"[*] Handshake complete\n");
                        _apiBlocks.Remove(args.RequestId);
                        return;
                    }

                    // If there is a case of missmatched requests and responses, and it doesn't look like a handshake, log it
                    if (Out != StreamWriter.Null)
                    {
                        Out.WriteLine($"[*] Request messages count ({args.RequestBlock.ParsedMessages.Count}) is different than the response messages count ({responseEnvelope.Returns.Count}).");

                        Out.WriteLine("Request:");
                        Out.WriteLine(args.RequestBlock);

                        Out.WriteLine("Response:");
                        Out.WriteLine("Not sure yet how to read this without knowing what it is.");

                        Out.WriteLine($"[*]\n");
                    }

                    // This section converts all of the responses into all of the request types that exist to see which one fits
                    int responseIndex = 0;

                    foreach (var responseBytes in responseEnvelope.Returns)
                    {
                        int start = responseIndex;
                        int end = responseIndex + requestTypes.Count - responseEnvelope.Returns.Count;

                        // Parse the responses
                        for (int i = start; i < end; i++)
                        {
                            var type = Type.GetType("POGOProtos.Networking.Responses." + requestTypes[i] + "Response,PoGo Proxy.Protocs");

                            var instance = (IMessage) Activator.CreateInstance(type);
                            instance.MergeFrom(responseBytes);

                            Out.WriteLine($"[*] Parsing as response {responseIndex} as {requestTypes[i]}");
                            Out.WriteLine(JsonConvert.SerializeObject(instance));
                        }

                        responseIndex++;
                        Out.WriteLine();
                    }
                    Out.WriteLine($"[*]\n");
                }

                // Parse the responses
                for (int i = 0; i < responseEnvelope.Returns.Count; i++)
                {
                    // Had to add assembly name to end of typeName string since protocs.cs files are in a different assembly
                    var type = Type.GetType("POGOProtos.Networking.Responses." + requestTypes[i] + "Response,PoGo Proxy.Protocs");

                    if (type == null)
                    {
                        if (Out != StreamWriter.Null) Out.WriteLine("[***] GetType returns null for requestType: " + requestTypes[i]);
                        if (Out != StreamWriter.Null) Out.WriteLine("[***] Check if POGOProtos.Networking.Requests.Messages." + requestTypes[i] + "Message exists.");
                        if (Out != StreamWriter.Null) Out.WriteLine("[*]\n");

                        responseBlock.ParsedMessages.Add(requestTypes[i], default(IMessage));
                    }
                    else
                    {
                        var instance = (IMessage)Activator.CreateInstance(type);
                        instance.MergeFrom(responseEnvelope.Returns[i]);

                        responseBlock.ParsedMessages.Add(requestTypes[i], instance);
                    }
                }

                // Have not seen this issue yet - here just in case
                if (!_apiBlocks.ContainsKey(responseEnvelope.RequestId))
                {
                    if (Out != StreamWriter.Null)
                    {
                        Out.WriteLine($"[*] Request doesn't exist with specified RequestId ({responseEnvelope.RequestId}).");
                        Out.WriteLine($"Response:\n{responseBlock}");
                        Out.WriteLine("[*]\n");
                    }
                }

                // Remove block from dictionary and invoke event handler
                args.ResponseBlock = responseBlock;
                _apiBlocks.Remove(args.RequestId);

                RequestHandled?.Invoke(this, args);
            }
        }

        /// <summary>
        /// Allows overriding default certificate validation logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private Task OnCertificateValidation(object sender, CertificateValidationEventArgs e)
        {
            //set IsValid to true/false based on Certificate Errors
            if (e.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
            {
                e.IsValid = true;
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Allows overriding default client certificate selection logic during mutual authentication
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private Task OnCertificateSelection(object sender, CertificateSelectionEventArgs e)
        {
            return Task.FromResult(0);
        }
    }
}
