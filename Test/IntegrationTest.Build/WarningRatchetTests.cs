using System;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    [Ignore]
    public class WarningRatchetTests {
        [TestMethod]
        [Ignore]
        public void WarningRatchet() {
            var ratchet = new WarningRatchet(new Microsoft.VisualStudio.Services.WebApi.VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials()));

            //you'll have to find a recent build ID (or some kind of stub tfs server) to use for this to work as data is cleaned up periodically
            var request = ratchet.CreateNewRequest("ExpertSuite", 854734, "refs/heads/master");

            var reporter = ratchet.GetWarningReporter(request);
            var warningReport = reporter.CreateWarningReport();

            var lastCountFiltered = reporter.GetAdjustedWarningCount();
            var lastGoodCount = ratchet.GetLastGoodBuildWarningCount(request);
            var count = ratchet.GetBuildWarningCount(request);

            Assert.IsFalse(String.IsNullOrWhiteSpace(warningReport));
            Assert.IsNotNull(lastGoodCount);
        }
    }

}
