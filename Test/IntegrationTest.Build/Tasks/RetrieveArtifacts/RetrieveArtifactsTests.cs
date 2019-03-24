using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.RetrieveArtifacts {
    [TestClass]
    public class RetrieveArtifactsTests : MSBuildIntegrationTestBase {

        [TestMethod]
        [Ignore]
        public void RetrieveArtifactsTest() {
            RunTarget(nameof(Aderant.Build.Tasks.ArtifactHandling.RetrieveArtifacts));
        }
    }
}
