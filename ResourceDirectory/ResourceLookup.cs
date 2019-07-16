using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Coral;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;
using Com.AugustCellars.CoAP.ResourceDirectory;
using Com.AugustCellars.CoAP.Util;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class ResourceLookup : Resource
    {
        private readonly EndpointRegister _root;

        public ResourceLookup(string name, EndpointRegister root) : base(name)
        {
            _root = root;
            Attributes.AddResourceType("core.rd-lookup-res");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationCbor);
            Attributes.AddContentType(MediaType.ApplicationJson);
            root.ResourceLookupResource = this;
        }


        protected override void DoGet(CoapExchange exchange)
        {
            Request req = exchange.Request;
            CBORObject items = null;
            StringBuilder sb = null;
            Dictionary<string, CBORObject> dict = null;
            int respContentType = MediaType.ApplicationLinkFormat;
            CoralBody coral = null;

            try {
                Filter filter = new Filter(req.UriQueries);
                int firstItem = 0;
                int lastItem = int.MaxValue;

                if (filter.Count != int.MaxValue) {
                    firstItem = filter.Count * filter.Page;
                    lastItem = firstItem + filter.Page - 1;
                }
                else if (filter.Page != 0) {
                    throw new Exception();
                }

                Response resp = Response.CreateResponse(req, StatusCode.Content);

                if (req.HasOption(OptionType.Accept)) {
                    foreach (Option acceptOption in req.GetOptions(OptionType.Accept)) {
                        switch (acceptOption.IntValue) {
                        case MediaType.ApplicationLinkFormat:
                            sb = new StringBuilder();
                            break;

#if false  // Work is dead?
                        case MediaType.ApplicationLinkFormatCbor:
                            items = CBORObject.NewArray();
                            dict = LinkFormat.CborAttributeKeys;
                            break;

                        case MediaType.ApplicationLinkFormatJson:
                            items = CBORObject.NewArray();
                            break;
#endif

                        case 65088:
                                coral = new CoralBody();
                            break;

                        default:
                            // Ignore value
                            break;

                        }

                        //  We found a value
                        if (sb != null || items != null || coral != null) {
                            respContentType = acceptOption.IntValue;
                            break;
                        }
                    }

                    if (sb == null && items == null && coral == null) {
                        exchange.Respond(StatusCode.NotAcceptable);
                        return;
                    }
                }
                else {
                    sb = new StringBuilder();
                }

                int itemCount = -1;

                foreach (EndpointNode ep in _root.ChildEndpointNodes) {
                    if (ep.IsDeleted) continue;

                    Uri baseUri = ep.BaseUrl;

                    if (respContentType == 65088) {
                        coral.Add(new CoralBase(baseUri));
                    }

                    foreach (IResource link in ep.Links) {
                        filter.ClearState();
                        if (!filter.Apply(link.Attributes)) {
                            if (!ep.ApplyFilter(filter, true, false)) {
                                filter.Href(link.Uri);
                            }
                        }
                        if (filter.Passes) {
                            itemCount += 1;
                            if (itemCount < firstItem || itemCount > lastItem) continue;
                            if (sb != null) {
                                LinkFormat.SerializeResource(link, sb, null, baseUri);
                                sb.Append(',');
                            }
                            else if (respContentType == MediaType.ApplicationCoralReef) {

                                LinkFormat.SerializeResourceInCoral(link, coral, dict, null, null);
                            }
#if false  // Work is dead?
                            else { 
                                LinkFormat.SerializeResource(link, items, dict, null, baseUri);
                            }
#endif
                        }
                    }
                }

                if (coral != null) {
                    resp.Payload = coral.EncodeToBytes(LinkFormat.ReefDictionary);
                }
                else if (sb != null) {
                    if (sb.Length > 0) sb.Remove(sb.Length - 1, 1);
                    resp.PayloadString = sb.ToString();
                }
                else if (dict == null) {
                    resp.PayloadString = items.ToJSONString();
                }
                else {
                    resp.Payload = items.EncodeToBytes();
                }

                resp.ContentFormat = respContentType;

                exchange.Respond(resp);
            }
            catch {
                exchange.Respond(StatusCode.BadRequest);
            }
        }
    }
}
