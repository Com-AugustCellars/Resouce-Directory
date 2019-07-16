using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using Resource = Com.AugustCellars.CoAP.Server.Resources.Resource;

namespace ResourceDirectory
{
    public class ResourceDirectoryResource : Resource
    {
        static Random _Random = new Random();
        private static string _CharSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private List<RdChild> _children = new List<RdChild>();

        /// <summary>
        /// Resource corresponding to a resource directory resource.
        /// </summary>
        /// <param name="resourceName"></param>
        public ResourceDirectoryResource(string resourceName) : base(resourceName)
        {
            Attributes.AddResourceType("core.rd");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(9999); // TODO - Add application/link-format+json and application/link-format+cbor
        }

        public string DefaultDomain { get; set; }

        /// <summary>
        /// Process POST messages
        /// </summary>
        /// <param name="exchange"></param>
        protected override void DoPost(CoapExchange exchange)
        {
            Request req = exchange.Request;

            try {

#if true
                RemoteResource links = RemoteResource.NewRoot(req.PayloadString);
#else
            RemoteResource links = RemoteResource.NewRoot(req.Payload, req.ContentType);
#endif

                //  URI variables - 
                //  ep - endpoint name - required
                //  d - domain - optional
                //  et - endpoint type - optional
                //  con - context - optional
                // 

                string childName;
                do {
                    childName = NewName();
                } while (GetChild(childName) != null);

                RdChild newChild = new RdChild(childName, links, req.UriQueries);

                if (newChild.Domain == null) newChild.Domain = DefaultDomain;
                if (newChild.Context == null) {
                    EndPoint ep = req.Source;
                    IPEndPoint ep2 = ep as IPEndPoint;
                    ;
                    if (ep2 != null) {
                        newChild.Context = $"coap://{ep2.Address}:{ep2.Port}";
                    }
                }

                foreach (RdChild child in _children) {
                    if (child.Domain == newChild.Domain && child.EndpointName == newChild.EndpointName) {
                        exchange.Respond(StatusCode.BadOption);
                        return;
                    }
                }

                Add(newChild);
                Response res = Response.CreateResponse(req, StatusCode.Created);

                res.LocationPath = Path + Name + "/" + childName;

                exchange.Respond(res);
            }
            catch {
                exchange.Respond(StatusCode.BadRequest);
            }
        }

        public bool HasEndpoint(string domain, string endpoint)
        {
            return true;
        }

        private string NewName()
        {
            return _CharSet.Select(c => _CharSet[_Random.Next(_CharSet.Length)]).Take(4).ToString();
        }
    }
}
