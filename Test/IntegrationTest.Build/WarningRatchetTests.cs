using System;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    public class WarningRatchetTests {
        [TestMethod]
        public void WarningRatchet() {
            var ratchet = new WarningRatchet(new VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials()));
            
            var request = ratchet.CreateNewRequest("ExpertSuite", 738400);

            var reporter = ratchet.GetWarningReporter(request);
            var warningReport = reporter.CreateWarningReport();

            Assert.IsNotNull(warningReport);
        }
    }
}