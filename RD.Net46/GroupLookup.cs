using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;
#if false

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class GroupLookup : Resource
    {
        private readonly GroupManager _groupManager;
        public GroupLookup(string name, GroupManager groupManager, EndpointRegister rd) : base(name)
        {
            Attributes.AddResourceType("core.rd-lookup-gp");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);

            _groupManager = groupManager;
        }

        protected override void DoGet(CoapExchange exchange)
        {
            Request req = exchange.Request;
            CBORObject items = null;
            StringBuilder sb = null;
            Dictionary<string, CBORObject> dict = null;
            int respContentType = MediaType.ApplicationLinkFormat;

            try {
                Filter filter = new Filter(req.UriQueries);
                int firstItem = 0;
                int lastItem = int.MaxValue;

                if (filter.Count != int.MaxValue) {
                    firstItem = filter.Count * filter.Page;
                    lastItem = firstItem + filter.Page - 1;
                }
                else if (filter.Page != 0) {
                    exchange.Respond(StatusCode.BadRequest);
                    return;
                }

                Response resp = Response.CreateResponse(req, StatusCode.Content);

                if (req.HasOption(OptionType.Accept)) {
                    foreach (Option acceptOption in req.GetOptions(OptionType.Accept)) {
                        switch (acceptOption.IntValue) {
                        case MediaType.ApplicationLinkFormat:
                            sb = new StringBuilder();
                            break;

                        case MediaType.ApplicationLinkFormatCbor:
                            items = CBORObject.NewArray();
                            dict = LinkFormat.CborAttributeKeys;
                            break;

                        case MediaType.ApplicationLinkFormatJson:
                            items = CBORObject.NewArray();
                            break;

                        default:
                            // Ignore value
                            break;

                        }

                        //  We found a value
                        if (sb != null || items != null) {
                            respContentType = acceptOption.IntValue;
                            break;
                        }
                    }

                    if (sb == null && items == null) {
                        exchange.Respond(StatusCode.NotAcceptable);
                        return;
                    }
                }
                else {
                    sb = new StringBuilder();
                    resp.ContentFormat = MediaType.ApplicationLinkFormat;
                }

                int itemCount = -1;

                foreach (GroupLeaf ep in _groupManager.AllGroups) {
                    filter.ClearState();
                    ep.ApplyFilter(filter, true);

                    if (filter.Passes) {
                        itemCount += 1;
                        if (itemCount < firstItem || itemCount > lastItem) continue;
                        if (sb != null) {
                            ep.SerializeResource(sb);
                            sb.Append(',');
                        }
                        else {
                            ep.SerializeResource(items, dict);
                        }
                    }
                }


                if (sb != null) {
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
#endif
