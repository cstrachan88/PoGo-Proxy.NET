using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Google.Protobuf;
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
        private readonly Dictionary<ulong, ResponseEventArgs> _responseBlocks;

        public string Ip { get; }
        public int Port { get; }
        public TextWriter Out { get; set; }

        public event EventHandler<ResponseEventArgs> ResponseReceived;

        public ProxyController(string ipAddress, int port)
        {
            _proxyServer = new ProxyServer();
            _responseBlocks = new Dictionary<ulong, ResponseEventArgs>();

            Ip = ipAddress;
            Port = port;
        }

        private void OnResponseReceived(ResponseEventArgs e)
        {
            ResponseReceived?.Invoke(this, e);
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
            var requests = new MessageBlock
            {
                RequestId = requestEnvelope.RequestId,
                MessageInitialized = callTime,
                MessageBlockType = MessageBlockType.Request,
                ParsedMessages = new Dictionary<RequestType, IMessage>()
            };

            // Parse all the requests
            foreach (Request request in requestEnvelope.Requests)
            {
                var type = Type.GetType("POGOProtos.Networking.Requests.Messages." + request.RequestType + "Message");

                var instance = (IMessage)Activator.CreateInstance(type);
                instance.MergeFrom(request.RequestMessage);

                requests.ParsedMessages.Add(request.RequestType, instance);
            }

            // TODO figure out why the requests are doubling up and what to do about it
            if (_responseBlocks.ContainsKey(requestEnvelope.RequestId))
            {
                if (Out != StreamWriter.Null)
                {
                    Out.WriteLine("[*]");
                    Out.WriteLine($"[*] Request Id({requestEnvelope.RequestId}) already exists.");

                    Out.WriteLine($"[*] Old request:\n{_responseBlocks[requestEnvelope.RequestId].Requests}");
                    Out.WriteLine($"[*] New request:\n{requests}");

                    Out.WriteLine("[*]");
                }
                //throw new ArgumentException($"Request Id ({requestEnvelope.RequestId}) already exists.");
            }

            // Initialize a new request/response paired block and track it to update response
            var args = new ResponseEventArgs
            {
                RequestId = requestEnvelope.RequestId,
                Requests = requests
            };
            _responseBlocks.Add(args.RequestId, args);

            //if (Out != StreamWriter.Null) Out.WriteLine(requests);
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
                var responses = new MessageBlock
                {
                    RequestId = responseEnvelope.RequestId,
                    MessageInitialized = callTime,
                    MessageBlockType = MessageBlockType.Response,
                    ParsedMessages = new Dictionary<RequestType, IMessage>()
                };

                // Grab the paired request
                var args = _responseBlocks[responses.RequestId];

                // Check if the requests and responses match up
                // TODO figure out why this is happening
                if (args.Requests.ParsedMessages.Count != responseEnvelope.Returns.Count)
                {
                    // Initial request is asking for 5 messages, but three of them are empty - so only getting back 2 responses
                    // These messages are not null - how to deal with this


                    if (Out != StreamWriter.Null)
                    {
                        Out.WriteLine("[*]");
                        Out.WriteLine($"[*] Request messages count ({args.Requests.ParsedMessages.Count}) is different than the response messages count ({responseEnvelope.Returns.Count}).");

                        Out.WriteLine($"[*] Request:\n{args.Requests}");

                        Out.WriteLine("[*]");
                    }
                    //throw new RankException("Request messages count is different than the response messages count.");
                }

                // Grab request types
                var requestTypes = args.Requests.ParsedMessages.Keys.ToList();

                // Parse the responses
                for (int i = 0; i < responseEnvelope.Returns.Count; i++)
                {
                    var type = Type.GetType("POGOProtos.Networking.Responses." + requestTypes[i] + "Response");

                    var instance = (IMessage)Activator.CreateInstance(type);
                    instance.MergeFrom(responseEnvelope.Returns[i]);

                    responses.ParsedMessages.Add(requestTypes[i], instance);
                }

                // TODO what scenarios would cause this
                if (!_responseBlocks.ContainsKey(responseEnvelope.RequestId))
                {
                    if (Out != StreamWriter.Null)
                    {
                        Out.WriteLine("[*]");
                        Out.WriteLine($"[*] Request doesn't exist with specified RequestId ({responseEnvelope.RequestId}).");

                        Out.WriteLine($"[*] Response:\n{responses}");

                        Out.WriteLine("[*]");
                    }
                    //throw new KeyNotFoundException($"Request doesn't exist with specified RequestId ({responseEnvelope.RequestId}).");
                }

                // Remove block from dictionary and invoke event handler
                args.Responses = responses;
                _responseBlocks.Remove(args.RequestId);
                OnResponseReceived(args);

                //if (Out != StreamWriter.Null) Out.WriteLine(responses);
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
