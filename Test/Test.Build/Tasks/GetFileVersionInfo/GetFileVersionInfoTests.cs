using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GetFileVersionInfo {
    [TestClass]
    public class GetFileVersionInfoTests : BuildTaskTestBase {
        [TestMethod]
        public void GetFileVersionInfoTest() {
            RunTarget("GetFileVersionInfo");
        }
    }
}