using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;

namespace ResourceDirectory
{
    class GroupLookup : Resource
    {
        private readonly GroupManager _groupManager;
        public GroupLookup(string name, GroupManager groupManager, ResourceDirectoryResource rd) : base(name)
        {
            Attributes.AddResourceType("core.rd-lookup-gp");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);

            _groupManager = groupManager
        }

        protected override void DoGet(CoapExchange exchange)
        {
            string domain = null;
            string group = null;

            foreach (string query in exchange.Request.UriQueries) {
                string[] items = query.Split('=');
                if (items.Length > 1 && items[1][0] == '"') items[1] = items[1].Substring(1, items[1].Length - 1);



                switch (items[0]) {
                    case "ep":
                    case "d":
                        domain = items[1];
                        break;

                    case "res":
                    case "gp":
                        group = items[1];
                        break;

                    case "page":
                    case "count":
                    case "rt":
                    case "et":
                        break;

                    default:
                        exchange.Respond(StatusCode.BadOption);
                        return;
                }
            }

            string result = "";

            foreach (GroupLeaf leaf in _groupManager.AllGroups) {
                if (domain != null && leaf.Domain != domain) continue;
                if (group != null && leaf.GroupName != group) continue;



            }
        }
    }
}
