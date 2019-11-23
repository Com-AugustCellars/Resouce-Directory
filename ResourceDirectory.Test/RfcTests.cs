using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Com.AugustCellars.CoAP.Server;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.ResourceDirectory;
using Com.AugustCellars.CoAP.Net;
using Com.AugustCellars.CoAP.Log;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Tls;

namespace ResourceDirectory.Test
{
    [TestClass]
    public class RfcTests
    {
        private CoapServer _server;
        private int _serverPort;
        CoapConfig _config = new CoapConfig();

        [TestInitialize]
        public void SetupServer()
        {
            LogManager.Level = LogLevel.Fatal;
            CreateServer();

        }

        [TestCleanup]
        public void ShutdownServer()
        {
            _server.Dispose();
        }

        [TestMethod]
        public void FindResourceDirectory()
        {
            CoapClient client = new CoapClient($"coap://localhost:{_serverPort}");
            List<WebLink> result = client.Discover("rt=core.rd*").ToList();
            int[] found = new int[3];

            Assert.AreEqual(3, result.Count);
            foreach (WebLink link in result) {
                List<string> ct = link.Attributes.GetContentTypes().ToList();
                Assert.AreEqual(1, ct.Count);
                Assert.AreEqual("40", ct[0]);

                List<string> rt = link.Attributes.GetResourceTypes().ToList();
                Assert.AreEqual(1, rt.Count);
                Assert.AreEqual("core.rd", rt[0].Substring(0, 7));

                switch (rt[0]) {
                    case "core.rd":
                        found[0] += 1;
                        Assert.AreEqual("/rd", link.Uri);
                        break;

                    case "core.rd-lookup-ep":
                        found[1] += 1;
                        Assert.AreEqual("/ep", link.Uri);
                        break;

                    case "core.rd-lookup-res":
                        found[2] += 1;
                        Assert.AreEqual("/res", link.Uri);
                        break;

                    default:
                        Assert.Fail("Unknown rt found");
                        break;
                }
            }

            foreach (int i in found) {
                Assert.AreEqual(1, i);
            }
        }

        [TestMethod]
        public void Register1()
        {
            Uri epBase =  new Uri("coap://[2001:db8:1::1]");
            Uri httpBase = new Uri("http://www.example.com");

            CoapClient client = new CoapClient($"coap://localhost:{_serverPort}/rd?ep=node1&base={epBase}");
            Response response = client.Post("</sensors/temp>;ct=41;rt=\"temperature-client\";if=\"sensor\",<http://www.example.com/sensors/temp>;anchor=\"/sensors/temp\";rel=\"describedby\"",
                MediaType.ApplicationLinkFormat);
            Assert.AreEqual(StatusCode.Created, response.StatusCode);
            Assert.AreNotEqual("", response.LocationPath);

            client.UriPath = "/res";
            client.UriQuery = "ep=node1";

            response = client.Get();
            Assert.AreEqual(StatusCode.Content, response.StatusCode);
            Assert.AreEqual(MediaType.ApplicationLinkFormat, response.ContentType);

            List<WebLink> links = LinkFormat.Parse(response.PayloadString).ToList();
            Assert.AreEqual(2, links.Count);
            foreach (WebLink link in links) {
                Uri href = new Uri(link.Uri);
                Assert.IsTrue(0 == Uri.Compare(epBase, href, UriComponents.SchemeAndServer | UriComponents.HostAndPort,  UriFormat.Unescaped,  StringComparison.InvariantCulture ) ||
                              0 == Uri.Compare(httpBase, href, UriComponents.SchemeAndServer | UriComponents.HostAndPort, UriFormat.Unescaped, StringComparison.InvariantCulture));
 
                Assert.IsTrue(link.Attributes.Contains("anchor"));
                href = new Uri(link.Attributes.GetValues("anchor").First());
                Assert.AreEqual(0, Uri.Compare(epBase, href, UriComponents.SchemeAndServer | UriComponents.HostAndPort, UriFormat.Unescaped, StringComparison.InvariantCulture));
                Assert.AreEqual(epBase.Authority, href.Authority);
            }
        }

        [TestMethod]
        public void Delete()
        {
            string item = "</res/1>;rt=sensor;ct=60";
            string[] locations = new string[20];

            CoapClient client = new CoapClient($"coap://localhost:{_serverPort}/rd");
            Response response;

            for (int i = 0; i < 20; i++) {
                client.UriQuery = $"ep=endpoint{i}&base=coap://[2001:db8:3::{i}]:61616";
                response = client.Post(item, MediaType.ApplicationLinkFormat);
                Assert.AreEqual(StatusCode.Created, response.StatusCode);
                locations[i] = response.LocationPath;
            }

            List<WebLink> links = client.Discover("/ep", "").ToList();
            Assert.AreEqual(20, links.Count);

            client.UriPath = locations[0];
            response = client.Delete();
            Assert.AreEqual(StatusCode.Deleted, response.StatusCode);

            links = client.Discover("/ep", "").ToList();
            Assert.AreEqual(19, links.Count);

            links = client.Discover("/ep", "ep=endpoint0").ToList();
            Assert.AreEqual(0, links.Count);

            client.UriPath = locations[19];
            response = client.Delete();
            Assert.AreEqual(StatusCode.Deleted, response.StatusCode);
            links = client.Discover("/ep", "ep=endpoint19").ToList();
            Assert.AreEqual(0, links.Count);

            for (int i = 1; i < 19; i++) {
                client.UriPath = locations[i];
                response = client.Delete();
                Assert.AreEqual(StatusCode.Deleted, response.StatusCode);
            }

            links = client.Discover("/ep").ToList();
            Assert.AreEqual(0, links.Count);
        }

        [TestMethod]
        public void FindEndpoint()
        {
            string item = "</res/1>;rt=sensor;ct=60,<res/2>;rt=sensor.foo;ct=20;test={0}";
            string[] locations = new string[20];
            List<WebLink> links;

            CoapClient client = new CoapClient($"coap://localhost:{_serverPort}/rd");
            Response response;

            for (int i = 0; i < 20; i++)
            {
                client.UriQuery = $"ep=endpoint{i}&base=coap://[2001:db8:3::{i}]:61616";
                response = client.Post(string.Format(item, i % 4), MediaType.ApplicationLinkFormat);
                Assert.AreEqual(StatusCode.Created, response.StatusCode);
                locations[i] = response.LocationPath;
            }

            links = client.Discover("/ep", "ep=endpoint0").ToList();
            Assert.AreEqual(1, links.Count);

            links = client.Discover("/ep", "test=2").ToList();
            Assert.AreEqual(5, links.Count);

        }

        public void Filtering()
        {

        }

        public void Observe()
        {

        }

        [TestMethod]
        public void PageTestsResource()
        {
            string epBase = "coap://[2001:db8:3::123]:61616";
            string item = "<coap://[2001:db8:3::123]:61616/res/{0}>;rt=sensor;ct=60;anchor=\"coap://[2001:db8:3::{0}]:61616\"";
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(item, 0);

            for (int i = 1; i < 20; i++) {
                sb.Append(",");
                sb.AppendFormat(item, i);
            }

            CoapClient client = new CoapClient($"coap://localhost:{_serverPort}/rd?ep=node1&base={epBase}");
            Response response = client.Post(sb.ToString(), MediaType.ApplicationLinkFormat);

            Assert.AreEqual(StatusCode.Created, response.StatusCode);

            List<WebLink> links = client.Discover("/res", "").ToList();
            Assert.AreEqual(20, links.Count);

            links = client.Discover("/res", "page=5&count=1").ToList();
            Assert.AreEqual(5, links.Count);

            links = client.Discover("/res", "page=6&count=3").ToList();
            Assert.AreEqual(2, links.Count);
        }

        [TestMethod]
        public void PageTestsEndpoint()
        {
            string item = "</res/1>;rt=sensor;ct=60";

            CoapClient client = new CoapClient($"coap://localhost:{_serverPort}/rd");
            Response response;

            for (int i = 0; i < 20; i++) {
                client.UriQuery = $"ep=endpoint{i}&base=coap://[2001:db8:3::{i}]:61616";
                response = client.Post(item, MediaType.ApplicationLinkFormat);
                Assert.AreEqual(StatusCode.Created, response.StatusCode);
            }

            List<WebLink> links = client.Discover("/ep", "").ToList();
            Assert.AreEqual(20, links.Count);

            links = client.Discover("/ep", "page=5&count=1").ToList();
            Assert.AreEqual(5, links.Count);

            links = client.Discover("/ep", "page=6&count=3").ToList();
            Assert.AreEqual(2, links.Count);
        }

        public void ChangeBase()
        {

        }

        private void CreateServer()
        {
            _server = new CoapServer();

            CoAPEndPoint endpoint = new CoAPEndPoint(_config);
            _server.AddEndPoint(endpoint);

            EndpointRegister ep = new EndpointRegister("rd", _server);
            _server.Add(ep);
//            NopResource nop = new NopResource("rd-lookup");
//            _server.Add(nop);

            EndpointLookup epLookup = new EndpointLookup("ep", ep);
            _server.Add(epLookup);
            ResourceLookup rsLookup = new ResourceLookup("res", ep);
            _server.Add(rsLookup);

            // Resource r = new Com.AugustCellars.CoAP.ResourceDirectory.SimpleRegistration(_server.EndPoints.First());
            // ep.Add(r);

            // r = new SimpleRegistration(_server.FindResource(""), ep);
            // _server.FindResource(".well-known").Add(r);

            _server.Start();
            _serverPort = ((System.Net.IPEndPoint)endpoint.LocalEndPoint).Port;
        }


    }
}
