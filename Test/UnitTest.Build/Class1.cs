using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class WarningWarningRatchetTests {

        [TestMethod]
        public void Test() {
            var buildWarningCount = new WarningRatchet(new VssConnection(new Uri("http://tfs:8080/tfs/Aderant/"), new VssClientCredentials())).GetBuildWarningCount("ExpertSuite", 635603);
        }
    }
}
