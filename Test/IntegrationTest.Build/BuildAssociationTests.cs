using System;
using Aderant.Build.Logging;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    public class BuildAssociationTests {
        [TestMethod]
        public void BuildAssociation() {
            var buildAssociation = new BuildAssociation(new FakeLogger(), new VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials()));
            buildAssociation.AssociateWorkItemsToBuildAsync("ExpertSuite", 641161).Wait();
        }
    }

    [TestClass]
    public class WarningRatchetTests {
        [TestMethod]
        public void WarningRatchet() {
            var ratchet = new WarningRatchet(new VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials()));
            ratchet.GetLastGoodBuildWarningCountAsync("ExpertSuite", 7623).Wait();
        }
    }
}
