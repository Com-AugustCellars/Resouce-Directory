using System.Linq;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Net;
using Com.AugustCellars.CoAP.ResourceDirectory;
using Com.AugustCellars.CoAP.Server;
using Com.AugustCellars.CoAP.Server.Resources;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class ResourceDirectory
    {
        public static System.Threading.Timer _CleanupTimer;

        public static void CreateResources(CoapServer server)
        {
            EndpointRegister ep = new EndpointRegister("rd", server);
            server.Add(ep);
            NopResource nop = new NopResource("rd-lookup");
            server.Add(nop);

            EndpointLookup epLookup = new EndpointLookup("ep", ep);
            nop.Add(epLookup);
#if false
            GroupManager gp = new GroupManager("rd-group", ep);
            server.Add(gp);
            ep.GroupMgr = gp;
            GroupLookup gp_lookup = new GroupLookup("gp", gp, ep);
            nop.Add(gp_lookup);
#endif
            ResourceLookup rsLookup = new ResourceLookup("res", ep);
            nop.Add(rsLookup);


            Resource r = new SimpleRegisterRequest(server.EndPoints.First());
            ep.Add(r);

            r = new SimpleRegistration(server.FindResource(""), ep);
            server.FindResource(".well-known").Add(r);

           
            _CleanupTimer = new System.Threading.Timer(EndpointRegister.Cleanup, ep, 5*60*1000, 5*60*1000);
        }

        class SimpleRegisterRequest : Resource
        {
            private readonly IEndPoint _epToUse;

            public SimpleRegisterRequest(IEndPoint ep) : base("post2")
            {
                _epToUse = ep;
            }

            protected override void DoPost(CoapExchange exchange)
            {
                //  The input string is supposed to be
                //  <url of resource directory> <endpoint name to use>

                string[] cmds = exchange.Request.PayloadString.Split();
                if (cmds.Length != 2) {
                    exchange.Respond(StatusCode.BadRequest);
                    return;
                }

                CoapClient c = new CoapClient(cmds[0]) {
                    UriPath = "/.well-known/core",
                    UriQuery = $"ep={cmds[1]}",
                    Timeout = 2000,
                    EndPoint = _epToUse
                };

                Response r = c.Post(new byte[] { }, MediaType.ApplicationLinkFormat);
                if (r == null || r.StatusCode != StatusCode.Changed) {
                    exchange.Respond(StatusCode.ProxyingNotSupported);
                }
                else {
                    exchange.Respond(StatusCode.Changed);
                }
            }
        }

        class NopResource : Resource
        {
            public NopResource(string name) : base(name)
            {
            }
        }
    }
}
