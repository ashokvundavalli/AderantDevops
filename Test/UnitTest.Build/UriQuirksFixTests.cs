using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class UriQuirksFixTests {
        [TestMethod]
        public void Forward_slash_is_not_unescaped() {
            PaketHttpMessageHandlerFactory.Configure();

            string http = "http://foo/%2f/1";
            var httpUri = new Uri(http);

            string https = "https://foo/%2f/1";
            var httpsUri = new Uri(https);

            Assert.AreEqual("/%2f/1", httpUri.PathAndQuery);
            Assert.AreEqual("/%2f/1", httpsUri.PathAndQuery);
        }
    }
}
