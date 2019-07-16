using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;
#if false

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class GroupLeaf : Resource
    {
        public ResourceAttributes RegistrationAttributes { get; private set; }
        public GroupManager GroupMgr { get; internal set; }

        public GroupLeaf(string resourceName, Request req, GroupManager mgr) : base(resourceName)
        {
            Server.Resources.ResourceAttributes newAttrs = new Server.Resources.ResourceAttributes();
            GroupMgr = mgr;

            foreach (string query in req.UriQueries) {
                string[] items = query.Split('=');
                string key = items[0];
                string value = (items.Length == 2) ? items[1] : null;
                if (value != null && value[0] == '"') {
                    //  M00BUG check to makes sure we don't need to strip this.
                }

                switch (items[0]) {
                case "gp":
                    GroupName = value;
                    break;

                case "d":
                    Domain = value;
                    break;

                case "base":
                    BaseUrl = new Uri(value);
                    break;

                default:
                    break;
                }

                newAttrs.Add(key, value);
            }

            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatCbor);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatJson);
            Attributes.Observable = false;
            Visible = false;

            RegistrationAttributes = newAttrs;
        }

        public Uri BaseUrl { get; private set; }
        public string Domain { get; private set; }
        public string GroupName { get; private set; }
        public List<string> EndPointNames { get; private set; } = new List<string>();

        protected override void DoDelete(CoapExchange exchange)
        {
            Parent.Remove(this);
            exchange.Respond(StatusCode.Deleted);
        }

        public StatusCode UpdateContent(byte[] body, int mediaType)
        {
            RemoteResource res;
            res = RemoteResource.NewRoot(body, mediaType);
            List<string> endpointsList = new List<string>();

            foreach (IResource r in res.Children) {
                if (r.Attributes.Count != 0) return StatusCode.BadRequest;

                IResource r2 = GroupMgr.EndpointMgr.Server.FindResource(r.Uri.Replace('/', ','));
                if (r2 == null || r2.Parent != GroupMgr.EndpointMgr) {
                    return StatusCode.BadRequest;
                }

                endpointsList.Add(r.Uri);
            }

            EndPointNames = endpointsList;

            return StatusCode.Created;
        }

        internal bool ApplyFilter(Filter filter, bool fSearchDown)
        {
            if (filter.Apply(RegistrationAttributes)) return true;

            if (fSearchDown) {
                foreach (EndpointNode ep in GroupMgr.EndpointMgr.ChildEndpointNodes) {
                    if (EndPointNames.Contains(ep.Uri) && !ep.IsDeleted) {
                        if (ep.ApplyFilter(filter, false, true)) return true;
                    }
                }
            }

            return false;
        }

        internal bool ContainsEndpoint(EndpointNode ep)
        {
            string epName = ep.Uri;
            return EndPointNames.Contains(epName);
        }

        public void SerializeResource(StringBuilder sb)
        {
            LinkFormat.SerializeResource(this, sb, RegistrationAttributes, null);
        }

        public void SerializeResource(CBORObject root, Dictionary<string, CBORObject> dict)
        {
            LinkFormat.SerializeResource(this, root, dict, RegistrationAttributes, null);
        }

        internal void RemoveEndpoint(EndpointNode ep)
        {
            if (ContainsEndpoint(ep)) {
                EndPointNames.Remove(ep.Uri);
            }
        }
    }
}
#endif
