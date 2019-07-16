using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;
#if false

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class GroupManager : Resource
    {

        private static string _CharSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        static Random _Random = new Random();
        public EndpointRegister EndpointMgr { get; internal set; }


        public GroupManager(string resourceName, EndpointRegister rd) : base(resourceName)
        {
            this.Attributes.AddResourceType("core.rd-group");
            EndpointMgr = rd;
        }

        public IEnumerable<GroupLeaf> AllGroups
        {
            get {
                foreach (IResource child in Children) {
                    if (child is GroupLeaf leaf) {
                        yield return leaf;
                    }
                }
            }
        }


        /// <summary>
        /// Create a group:  Posting here with a list will either replace an existing 
        /// group or create a new one.
        /// Template: {+rd-group}{?gp,d,con}
        /// gp = group name - MANDITORY
        /// d = domain - OPTIONAL
        /// con = Multicast Address - OPTIONAL
        /// </summary>
        /// <param name="exchange"></param>
        protected override void DoPost(CoapExchange exchange)
        {
            GroupLeaf leaf = null;
            GroupLeaf leafOld = null;
            string groupName = null;
            string domain = null;

            try {
                foreach (string opt in exchange.Request.UriQueries) {
                    if (opt.StartsWith("gp=")) {
                        groupName = opt.Substring(3, opt.Length - 3);
                        if (groupName[0] == '"') groupName = groupName.Substring(1, groupName.Length - 1);
                    }
                    else if (opt.StartsWith("d=")) {
                        domain = opt.Substring(2);
                    }
                }

                if (domain == null) {
                    domain = EndpointMgr.DefaultDomain;
                }

                foreach (IResource res in this.Children) {
                    leaf = res as GroupLeaf;
                    if (leaf != null) {
                        if (leaf.GroupName == groupName && leaf.Domain == domain) {
                            leafOld = leaf;
                            break;
                        }
                    }
                }

                string childName;
                if (leafOld != null) {
                    childName = leafOld.Name;
                }
                else {
                    do {
                        childName = NewName();
                    } while (GetChild(childName) != null);
                }

                try {
                    leaf = new GroupLeaf(childName, exchange.Request, this);
                }
                catch {
                    exchange.Respond(StatusCode.BadOption);
                    return;
                }

                if (exchange.Request.HasOption(OptionType.ContentType)) {
                    switch (exchange.Request.ContentType) {
                    case MediaType.ApplicationLinkFormat:
                    case MediaType.ApplicationLinkFormatCbor:
                    case MediaType.ApplicationLinkFormatJson:
                        leaf.UpdateContent(exchange.Request.Payload, exchange.Request.ContentFormat);
                        break;

                    default:
                        exchange.Respond(StatusCode.BadOption);
                        return;
                    }
                }
                else {
                    leaf.UpdateContent(exchange.Request.Payload, MediaType.ApplicationLinkFormat);
                }

                /*
                 * Does not appear to be my job any more
                 * 
                foreach (string ep in leaf.EndPointNames) {
                    if (!_rd.HasEndpoint(leaf.Domain, ep)) {
                        exchange.Respond(StatusCode.BadRequest, "EP missing");
                        return;
                    }
                }
                */
                
                if (leafOld != null) Remove(leafOld);
                Add(leaf);

                exchange.LocationPath = leaf.Uri;
                exchange.Respond(StatusCode.Created);
            }
            catch (Exception e) {
                exchange.Respond(StatusCode.InternalServerError, e.Message);
            }
        }

        private string NewName()
        {
            /*
            return _CharSet.Select(c => _CharSet[_Random.Next(_CharSet.Length)]).Take(4).ToString();
            */
            string str = "";
            for (int i = 0; i < 4; i++) {
                str += _CharSet[_Random.Next(_CharSet.Length)];
            }

            return str;
        }

    }
}
#endif
