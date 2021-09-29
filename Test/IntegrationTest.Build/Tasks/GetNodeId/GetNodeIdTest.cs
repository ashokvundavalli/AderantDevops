using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GetNodeId {
    [TestClass]
    public class GetNodeIdTest : MSBuildIntegrationTestBase {

        [TestMethod]
        public void RunGetNodeId() {
            RunTarget("RunGetNodeId");

            // Query build state to see what node was used
            var projectPropertyInstances =
                GetResult().ProjectStateAfterBuild.Properties.First(s => s.Name == nameof(Aderant.Build.Tasks.GetNodeId.NodeId));

            Assert.AreNotEqual("-1", projectPropertyInstances.EvaluatedValue);
        }
    }
}
