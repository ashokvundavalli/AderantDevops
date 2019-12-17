using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.ReadAssemblyInfo {

    [TestClass]
    public class ReadAssemblyInfo : MSBuildIntegrationTestBase {
        [TestMethod]
        public void ReadAssemblyInfoTest() {
            RunTarget("ReadAssemblyInfo");
        }
    }
}