using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.RetrieveArtifacts {
    [TestClass]
    public class RetrieveArtifactsTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void RetrieveArtifactsTest() {
            BuildOperationContextTask.InternalContext = new BuildOperationContext();

            RunTarget(nameof(Aderant.Build.Tasks.RetrieveArtifacts)); 
        }
    }
}
