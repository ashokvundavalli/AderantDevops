using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class GetFileVersionInfoTests : BuildTaskTestBase {
        [TestMethod]
        public void GetFileVersionInfoTest() {
            RunTarget("GetFileVersionInfo");
        }
    }

}