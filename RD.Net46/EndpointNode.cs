using System;
using System.Collections.Generic;
using System.Text;
using xx = Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;
using Resource = Com.AugustCellars.CoAP.Server.Resources.Resource;
using ResourceAttributes = Com.AugustCellars.CoAP.Server.Resources.ResourceAttributes;


namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    internal class EndpointNode : Resource
    {
        private xx.RemoteResource _remote;
        private int _lifeTime = 86400; // 24 - hours

        public ResourceAttributes RegistrationAttributes { get; private set; }
        private readonly EndpointRegister _owner;

        public EndpointNode(string name, xx.RemoteResource root, IEnumerable<string> uriQueries, EndpointRegister owner) : base(name, false)
        {
            Reload(root, uriQueries);
            _owner = owner;

#if false
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatCbor);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatJson);
            Attributes.Observable = true;
#endif
            Visible = false;
        }

        public void Reload(xx.RemoteResource root, IEnumerable<string> uriQueries)
        {
            ResourceAttributes newAttrs = new ResourceAttributes();

            _lifeTime = 86400;
            foreach (string query in uriQueries) {
                int eq = query.IndexOf('=');
                string key = eq == -1 ? query : query.Substring(0, eq);
                string value = (eq == -1) ? null : query.Substring(eq + 1);
                if (value != null && value[0] == '"') {
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
                    _lifeTime = Int32.Parse(value);
                    break;

                case "base":
                    if (value == null) throw new Exception();
                    BaseUrl = new Uri(value);
                    if (BaseUrl.LocalPath != null && BaseUrl.LocalPath != "") {
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

            Changed();
        }

        public Uri BaseUrl { get; set; }
        public String Domain { get; set; }
        public String EndpointName { get; private set; }
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
                Dictionary<string, CBORObject> dict = null;
                int retContentType = MediaType.Any;

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
                            retContentType = acceptOption.IntValue;
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
                        else {
                            LinkFormat.SerializeResource(link, items, dict, null, null);
                        }
                    }
                }

                if (sb != null) {
                    sb.Remove(sb.Length - 1, 1);
                    resp.PayloadString = sb.ToString();
                }
                else if (dict == null) {
                    resp.PayloadString = items.ToJSONString();
                }
                else {
                    resp.Payload = items.EncodeToBytes();
                }

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
            Server.Resources.ResourceAttributes newAttrs = new ResourceAttributes();
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

#if false
        // No longer supported
        protected override void DoPatch(CoapExchange exchange)
        {
            Request req = exchange.Request;
            CBORObject patch;

            if (req.HasOption(OptionType.ContentType)) {
                switch (req.ContentType) {
                    case 9998: // application/merge-patch+json
                        patch = CBORObject.FromJSONString(req.PayloadString);
                        break;

                    case 9997: // application/merge-patch+cbor
                        patch = CBORObject.DecodeFromBytes(req.Payload);
                        break;

                    default:
                        exchange.Respond(StatusCode.BadRequest);
                        return;
                }
            }
            else {
                //  Assume it is application/merge-patch+json for no reason
                patch = CBORObject.FromJSONString(req.PayloadString);
            }

            if (patch.Type == CBORType.Array) {
                DoPatchAdd(patch, exchange);
            }
            else if (patch.Type == CBORType.Map) {
                DoPatchUpdate(patch, exchange);
            }
            else {
                exchange.Respond(StatusCode.BadRequest);
            }
        }
#endif

        protected override void DoDelete(CoapExchange exchange)
        {
            // M00BUG - DELETE ME NOW not later.
            ExpireTime = DateTime.MinValue;
            exchange.Respond(StatusCode.Deleted);
        }

        internal bool ApplyFilter(Filter filterList, bool searchUp, bool searchDown)
        {
            if (filterList.Apply(this.RegistrationAttributes)) return true;
            // if (filterList.Apply(this.Attributes)) return true;

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

        internal void GetLink(StringBuilder sb)
        {
            LinkFormat.SerializeResource(this, sb, RegistrationAttributes);
            sb.Append(",");
        }

        internal void GetLink(CBORObject array, Dictionary<string, CBORObject> cborDictionary)
        {
            LinkFormat.SerializeResource(this, array, cborDictionary, RegistrationAttributes);
        }

#if false
        // Patch goes away

        private void DoPatchAdd(CBORObject patch, CoapExchange exchange)
        {
            
        }

        private void DoPatchUpdate(CBORObject patch, CoapExchange exchage)
        {
            if (patch.ContainsKey("href") || patch.ContainsKey("rel") || patch.ContainsKey("anchor")) {
                exchage.Respond(StatusCode.BadRequest);
                return;
            }

        //    IList<WebLink> patchList = _remote.Where(exchange.Request.UriQueries);

         //   foreach (WebLink link in patchList) {
                
       //     }

        }



        private WebLink MergePatch(WebLink target, CBORObject patch)
        {
            if (!VerifyAndPatch(patch)) {
                //  Invalid body.
                return null;
            }

            foreach (CBORObject name in patch.Keys) {
                CBORObject value = patch[name];
                if (value.IsNull) {
                    if (target.Attributes.Contains(name.AsString())) {
                        target.Attributes.Clear(name.AsString());
                    }
                }
                else if (value.IsTrue) {
                    target.Attributes.Clear(name.AsString());
                    target.Attributes.Add(name.AsString());
                }
                else if (value.Type == CBORType.Array) {
                    target.Attributes.Clear(name.AsString());
                    for (int i = 0; i < value.Count; i++) {
                        CBORObject v2 = value[i];
                        if (v2.IsTrue) target.Attributes.Add(name.AsString());
                        else target.Attributes.Add(name.AsString(), v2.AsString());
                    }
                }
                else if (value.Type == CBORType.TextString) {
                    target.Attributes.Clear(name.AsString());
                    target.Attributes.Set(name.AsString(), value.AsString());
                }
                else {
                    //  This is an error - we should have already found it.
                    ;
                }
            }

            return target;
        }

        private bool VerifyAndPatch(CBORObject patch)
        {
            List<CBORObject> keys = patch.Keys.ToList();

            foreach (CBORObject key in keys) {
                CBORObject value = patch[key];

                // TODO - deal with cbor to json mapping

                if (value.IsNull || value.IsTrue) continue;
                if (value.Type == CBORType.Array) {
                    for (int i = 0; i < value.Count; i++) {
                        if (value.IsNull || value.IsTrue || value.Type == CBORType.TextString) continue;
                        return false;
                    }
                }
                else if (value.Type == CBORType.TextString) {

                }
                else return false;
            }

            return true;
        }
#endif
    }
}
