using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace PoGo_Proxy
{
    public class ProxyController
    {
        private readonly ProxyServer _proxyServer;

        private const string Ip = "192.168.0.19";
        private const int Port = 8081;

        public ApiLogger ApiLogger { get; }

        public ProxyController()
        {
            _proxyServer = new ProxyServer();
            ApiLogger = new ApiLogger();
        }

        public void Start()
        {
            // Link up handlers
            _proxyServer.BeforeRequest += OnRequest;
            _proxyServer.BeforeResponse += OnResponse;
            _proxyServer.ServerCertificateValidationCallback += OnCertificateValidation;
            _proxyServer.ClientCertificateSelectionCallback += OnCertificateSelection;

            // An explicit endpoint is where the client knows about the existance of a proxy
            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Parse(Ip), Port, true);
            _proxyServer.AddEndPoint(explicitEndPoint);

            // Start proxy server
            _proxyServer.Start();

            Console.WriteLine($"[+++] Listening at {explicitEndPoint.IpAddress}:{explicitEndPoint.Port} ");
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

            Console.WriteLine("[---] Server stopped");
        }

        public async Task OnRequest(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.RequestUri.Host != "pgorelease.nianticlabs.com") return;

            var callTime = DateTime.Now;
            byte[] bodyBytes = await e.GetRequestBody();
            var codedInputStream = new CodedInputStream(bodyBytes);
            var requestEnvelope = RequestEnvelope.Parser.ParseFrom(codedInputStream);

            var parsedMessages = ApiLogger.AddRequest(requestEnvelope, callTime);

            Console.WriteLine(parsedMessages);
        }


        public async Task OnResponse(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.RequestUri.Host != "pgorelease.nianticlabs.com") return;

            if (e.WebSession.Response.ResponseStatusCode == "200")
            {
                var callTime = DateTime.Now;
                byte[] bodyBytes = await e.GetResponseBody();
                var codedInputStream = new CodedInputStream(bodyBytes);
                var responseEnvelope = ResponseEnvelope.Parser.ParseFrom(codedInputStream);

                var parsedMessages = ApiLogger.AddResponse(responseEnvelope, callTime);

                Console.WriteLine(parsedMessages);
            }
        }

        /// <summary>
        /// Allows overriding default certificate validation logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public Task OnCertificateValidation(object sender, CertificateValidationEventArgs e)
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
        public Task OnCertificateSelection(object sender, CertificateSelectionEventArgs e)
        {
            return Task.FromResult(0);
        }
    }
}
