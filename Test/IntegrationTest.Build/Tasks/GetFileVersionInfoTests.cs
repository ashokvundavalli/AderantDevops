using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("IntegrationTest.targets")]
    [DeploymentItem("Aderant.Build.Common.targets")]
    public class GetFileVersionInfoTests : BuildTaskTestBase {
        [TestMethod]
        public void GetFileVersionInfoTest() {
            RunTarget("GetFileVersionInfo");
        }
    }
}