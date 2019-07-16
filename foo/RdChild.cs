using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using xx = Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using Org.BouncyCastle.Bcpg.OpenPgp;
using PeterO.Cbor;
using Resource = Com.AugustCellars.CoAP.Server.Resources.Resource;

namespace ResourceDirectory
{
    class RdChild : Resource
    {
        private xx.RemoteResource _remote;
        private string _endPointType;
        private int _lifeTime = 86400; // 24 - hours
        private bool _deleted;
        

        public RdChild(string name, xx.RemoteResource root, IEnumerable<string> uriQueries) : base(name, false)
        {
            _remote = root;
            foreach (string query in uriQueries) {
                int eq = query.IndexOf('=');
                string key = eq == -1 ? query : query.Substring(0, eq);
                switch (key) {
                    case "ep":
                        EndpointName = query.Substring(eq + 1);
                        break;

                    case "d":
                        Domain = query.Substring(eq + 1);
                        break;

                    case "et":
                        _endPointType = query.Substring(eq + 1);
                        break;

                    case "lt":
                        _lifeTime = Int32.Parse(query.Substring(eq+1));
                        break;

                    case "con":
                        Context = query.Substring(0, eq);
                        break;
                }


            }

            ExpireTime = DateTime.Now + TimeSpan.FromSeconds(_lifeTime);
        }

        public String Context { get; set; }
        public String Domain { get; set; }
        public String EndpointName { get; private set; }
        public DateTime ExpireTime { get; private set; }
    

        protected override void DoGet(CoapExchange exchange)
        {
            Response resp = Response.CreateResponse(exchange.Request, StatusCode.Content);
            resp.Payload = LinkFormat.Serialize(_remote, exchange.Request.UriQueries, exchange.Request.ContentType);
            
            exchange.Respond(resp);
        }

        protected override void DoPost(CoapExchange exchange)
        {
            Request req = exchange.Request;

            foreach (string query in req.UriQueries) {
                int eq = query.IndexOf('=');
                string key = eq == -1 ? query : query.Substring(0, eq);
                if (eq == -1) {
                    exchange.Respond(StatusCode.BadOption);
                    return;
                }
                switch (key) {
                    case "lt":
                        _lifeTime = Int32.Parse(query.Substring(eq+1));
                        break;

                    case "con":
                        Context = query.Substring(eq+1);
                        break;
                }
                
            }

            if (req.Payload.Length > 0) {
                IEnumerable<WebLink> links = LinkFormat.Parse(req.PayloadString);


                xx.Resource[] x = _remote.GetSubResources();

                Dictionary<string, xx.Resource> conflicts = new Dictionary<string, xx.Resource>();



            }

            ExpireTime = DateTime.Now + TimeSpan.FromSeconds(_lifeTime);
            exchange.Respond(StatusCode.Changed);
        }

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

        protected override void DoDelete(CoapExchange exchange)
        {
            ExpireTime = DateTime.MinValue;
            exchange.Respond(StatusCode.Deleted);
        }

        private void DoPatchAdd(CBORObject patch, CoapExchange exchange)
        {
            
        }

        private void DoPatchUpdate(CBORObject patch, CoapExchange exchage)
        {
            if (patch.ContainsKey("href") || patch.ContainsKey("rel") || patch.ContainsKey("anchor")) {
                exchage.Respond(StatusCode.BadRequest);
                return;
            }

            IList<WebLink> patchList = _remote.Where(exchage.Request.UriQueries);

            foreach (WebLink link in patchList) {
                
            }

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
    }
}
