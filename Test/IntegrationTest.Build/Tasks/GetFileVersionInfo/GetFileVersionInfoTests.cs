using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GetFileVersionInfo {
    [TestClass]
    public class GetFileVersionInfoTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void GetFileVersionInfoTest() {
            RunTarget("GetFileVersionInfo");
        }
    }
}