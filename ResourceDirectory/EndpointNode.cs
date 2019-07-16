using System;
using System.Collections.Generic;
using System.Text;
using Com.AugustCellars.CoAP.Coral;
using CEndPoint = Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;
using Resource = Com.AugustCellars.CoAP.Server.Resources.Resource;
using ResourceAttributes = Com.AugustCellars.CoAP.Server.Resources.ResourceAttributes;


namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    internal class EndpointNode : Resource
    {
        private CEndPoint.RemoteResource _remote;
        private int _lifeTime = 86400; // 24 - hours

        public ResourceAttributes RegistrationAttributes { get; private set; }
        private readonly EndpointRegister _owner;

        public EndpointNode(string name, CEndPoint.RemoteResource root, IEnumerable<string> uriQueries, EndpointRegister owner) : base(name, false)
        {
            Reload(root, uriQueries);
            _owner = owner;

            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationCoralReef);
#if false
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatCbor);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatJson);
#endif
            Attributes.Observable = true;
            Visible = false;
        }

        public void Reload(CEndPoint.RemoteResource root, IEnumerable<string> uriQueries)
        {
            ResourceAttributes newAttrs = new ResourceAttributes();

            _lifeTime = 90000;
            foreach (string query in uriQueries) {
                string[] vals = query.Split(new char[]{'='}, 2);
                string key = vals[0];
                string value = vals.Length == 2 ? vals[1] : null;

                if (value != null && value.Length >= 2 && value[0] == '"') {
                    value = value.Substring(1, value.Length - 1);
                }

                switch (key) {
                case "ep":
                    EndpointName = value ?? throw new Exception();
                    break;

                case "d":
                    Domain = value ?? throw new Exception();
                    break;

                case "lt":
                    if (value == null) throw new Exception();
                    _lifeTime = int.Parse(value);
                    break;

                case "base":
                    if (value == null) throw new Exception();
                    BaseUrl = new Uri(value);
                    if (BaseUrl.LocalPath == "") {
                        throw new Exception();
                    }
                    break;

                case "count":
                case "page":
                    throw new Exception();
                }

                newAttrs.Add(key, value);
            }

            _remote = root;
            RegistrationAttributes = newAttrs;

            ExpireTime = DateTime.Now + TimeSpan.FromSeconds(_lifeTime);
        }

        public Uri BaseUrl { get; set; }
        public string Domain { get; set; }
        public string EndpointName { get; private set; }
        public DateTime ExpireTime { get; private set; }
        public bool IsDeleted
        {
            get => ExpireTime < DateTime.Now;
        }

        public IEnumerable<IResource> Links => _remote.Children;

        protected override void DoGet(CoapExchange exchange)
        {
            try {
                Request req = exchange.Request;
                StringBuilder sb = null;
                CBORObject items = null;
                CoralBody coralBody = null;
#if false
                Dictionary<string, CBORObject> dict = null;
#endif
                int retContentType = MediaType.Any;

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

                        case MediaType.ApplicationCoralReef:
                            coralBody = new CoralBody();
                            break;

                        default:
                            // Ignore value
                            break;

                        }

                        //  We found a value
                        if (sb != null || items != null || coralBody != null) {
                            retContentType = acceptOption.IntValue;
                            break;
                        }
                    }

                    if (sb == null && items == null && coralBody == null) {
                        exchange.Respond(StatusCode.NotAcceptable);
                        return;
                    }
                }
                else {
                    sb = new StringBuilder();
                    retContentType = MediaType.ApplicationLinkFormat;
                }

                Filter filter = new Filter(req.UriQueries);

                Response resp = Response.CreateResponse(exchange.Request, StatusCode.Content);

                foreach (IResource link in Links) {
                    filter.ClearState();
                    if (!filter.Apply(link.Attributes)) {
                        ApplyFilter(filter, true, false);
                    }

                    if (filter.Passes) {
                        if (sb != null) {
                            LinkFormat.SerializeResource(link, sb, null, null);
                            sb.Append(',');
                        }
                        else if (coralBody != null) {
                            LinkFormat.SerializeCoral(link, null);
                        }
#if false
                        else {
                            LinkFormat.SerializeResource(link, items, dict, null, null);
                        }
#endif
                    }
                }

                if (sb != null) {
                    sb.Remove(sb.Length - 1, 1);
                    resp.PayloadString = sb.ToString();
                }
                else if (coralBody != null) {
                    resp.Payload = coralBody.EncodeToBytes(LinkFormat.ReefDictionary);
                }
#if false // dead
                else if (dict == null) {
                    resp.PayloadString = items.ToJSONString();
                }
                else {
                    resp.Payload = items.EncodeToBytes();
                }
#endif

                resp.ContentFormat = retContentType;

                exchange.Respond(resp);
            }
            catch (Exception) {
                exchange.Respond(StatusCode.InternalServerError);
            }
        }

        protected override void DoPost(CoapExchange exchange)
        {
            Request req = exchange.Request;
            ResourceAttributes newAttrs = new ResourceAttributes();
            foreach (string key in RegistrationAttributes.Keys) {
                foreach (string value in RegistrationAttributes.GetValues(key)) {
                    newAttrs.Add(key, value);
                }
            }

            int newLifetime = 0;
            Uri newBase = null;

            foreach (string query in req.UriQueries) {
                int eq = query.IndexOf('=');
                string key = eq == -1 ? query : query.Substring(0, eq);
                string value = (eq == -1) ? null : query.Substring(eq + 1);
                if (value != null && value[0] == '"') {
                    value = value.Substring(1, value.Length - 1);
                }

                switch (key) {
                    case "lt":
                        newLifetime = int.Parse(query.Substring(eq+1));
                        break;

                    case "base":
                        newBase = new Uri( query.Substring(eq+1));
                        break;

                    case "ep":
                    case "d":
                        exchange.Respond(StatusCode.BadOption);
                        return;
                }

                newAttrs.Add(key, value);
            }

            if (req.Payload != null && req.Payload.Length > 0) {
                exchange.Respond(StatusCode.BadRequest);
                return;
            }

            RegistrationAttributes = newAttrs;
            if (newLifetime != 0) _lifeTime = newLifetime;
            if (newBase != null) BaseUrl = newBase;

            ExpireTime = DateTime.Now + TimeSpan.FromSeconds(_lifeTime);
            exchange.Respond(StatusCode.Changed);
            _owner.ContentChanged();
        }

        protected override void DoDelete(CoapExchange exchange)
        {
            ExpireTime = DateTime.MinValue;
            exchange.Respond(StatusCode.Deleted);
        }

        internal bool ApplyFilter(Filter filterList, bool searchUp, bool searchDown)
        {
            if (filterList.Apply(RegistrationAttributes)) return true;

            if (searchDown) {
                foreach (IResource link in _remote.Children) {
                    if (filterList.Apply(link.Attributes)) return true;
                }
            }

#if false
            if (searchUp && _owner.GroupMgr != null) {
                foreach (GroupLeaf g in _owner.GroupMgr.AllGroups) {
                    if (g.ContainsEndpoint(this)) {
                        if (g.ApplyFilter(filterList, false)) return true;
                    }
                }
            }
#endif


            return false;
        }

        internal void GetLink(CoralBody coral)
        {
            LinkFormat.SerializeResourceInCoral(this, coral, null, RegistrationAttributes, null, true);
        }

        internal void GetLink(StringBuilder sb)
        {
            LinkFormat.SerializeResource(this, sb, RegistrationAttributes);
            sb.Append(",");
        }

#if false  // Work is dead?
        internal void GetLink(CBORObject array, Dictionary<string, CBORObject> cborDictionary, int retContentType)
        {
            if (retContentType == MediaType.ApplicationLinkFormatCbor) {
                LinkFormat.SerializeResource(this, array, cborDictionary, RegistrationAttributes);
            }
            else {
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
                // LinkFormat.SerializeResourceInCoral(this, array, cborDictionary, RegistrationAttributes);
            }
        }
#endif
    }
}
