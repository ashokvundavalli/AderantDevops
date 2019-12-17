using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("Resources\\AssemblyInfo.cs")]
    public class ReadAssemblyInfo : BuildTaskTestBase {
        [TestMethod]
        public void ReadAssemblyInfoTest() {
            RunTarget("ReadAssemblyInfo");
        }
    }
}