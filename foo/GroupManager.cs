using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;

namespace ResourceDirectory
{
    public class GroupManager : Resource
    {

        private static string _CharSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        static Random _Random = new Random();
        private ResourceDirectoryResource _rd;


        public GroupManager(string resourceName, ResourceDirectoryResource rd) : base(resourceName)
        {
            this.Attributes.AddResourceType("core.rd-group");
            _rd = rd;
        }

        public IEnumerable<GroupLeaf> AllGroups
        {
            get
            {
                foreach (IResource child in Children) {
                    if (child is GroupLeaf leaf) {
                        yield return leaf;
                    }
                }
            }
        }

        protected override void DoPost(CoapExchange exchange)
        {

            GroupLeaf leaf = null;
            GroupLeaf leafOld = null;
            string groupName = null;

            foreach (string opt in exchange.Request.UriQueries) {
                if (opt.StartsWith("gp=")) {
                    groupName = opt.Substring(3, opt.Length - 4);
                    if (groupName[0] == '"') groupName = groupName.Substring(1, groupName.Length - 1);
                    break;
                }

            }

            foreach (IResource res in this.Children) {
                   leaf = res as GroupLeaf;
                if (leaf != null) {
                    if (leaf.GroupName == groupName) {
                        leafOld = leaf;
                        break;
                    } 
                }
            }

                string childName;
                do {
                    childName = NewName();
                } while (GetChild(childName) != null);

                try {
                    leaf = new GroupLeaf(childName, exchange.Request);
                }
                catch {
                    exchange.Respond(StatusCode.BadOption);
                    return;
                }
            

            if (exchange.Request.HasOption(OptionType.ContentType)) {
                switch (exchange.Request.ContentType) {
                    case MediaType.ApplicationLinkFormat:
                        break;

                    default:
                        exchange.Respond(StatusCode.BadOption);
                        return;
                }
            }
            else {
                leaf.UpdateContent(exchange.Request.PayloadString);
            }

            foreach (string ep in leaf.EndPointNames) {
                if (!_rd.HasEndpoint(leaf.Domain, ep)) {
                    exchange.Respond(StatusCode.BadRequest, "EP missing");
                    return;
                }
            }

            if (leafOld != null) Remove(leafOld);
            Add(leaf);

            exchange.LocationPath = leaf.Path;
            exchange.Respond(StatusCode.Created);
        }

        private string NewName()
        {
            return _CharSet.Select(c => _CharSet[_Random.Next(_CharSet.Length)]).Take(4).ToString();
        }

    }
}
