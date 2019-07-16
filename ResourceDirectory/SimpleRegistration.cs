using System;
using System.Net;
using System.Net.Sockets;
using Com.AugustCellars.CoAP.Server.Resources;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class SimpleRegistration : DiscoveryResource
    {
        private readonly EndpointRegister _registerAt;
        public SimpleRegistration(IResource root, EndpointRegister register) : base(root)
        {
            _registerAt = register;
        }

        protected override void DoPost(CoapExchange exchange)
        {
            exchange.Respond(StatusCode.Changed);

            try {
                System.Net.EndPoint ep = exchange.Request.Source;
                IPEndPoint ep2 = ep as IPEndPoint;

                string context;

                if (ep2 != null) {
                    if (ep2.AddressFamily == AddressFamily.InterNetworkV6) {
                        //  SHOULD reject link local addresses  M00BUG - this should be configurable.
                        if (ep2.Address.IsIPv6LinkLocal) {
                            exchange.Respond(StatusCode.BadRequest);
                            return;
                        }
                        context = $"coap://[{ep2.Address}]:{ep2.Port}";
                    }
                    else {
                        context = $"coap://{ep2.Address}:{ep2.Port}";
                    }
                }
                else {
                    //  Can't do anything with this address
                    exchange.Respond(StatusCode.BadRequest);
                    return;
                }

                //  Look to see if we have a cached version already

                Uri u = new Uri(context);

                CoapClient c = new CoapClient(u) {
                    UriPath = "/.well-known/core",
                    Timeout = 2000
                };

                Response r = c.Get();
                if (r != null && r.StatusCode == StatusCode.Content) {
                    _registerAt.RegisterEndpoint(exchange.Request.UriQueries, r.Payload, r.ContentType, r.Source);
                }
            }
            catch (Exception e) {
                Console.WriteLine("Error doing SimpleRegistration::DoPost " + e);
            }
        }
    }
}
