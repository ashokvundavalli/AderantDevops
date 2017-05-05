using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("IntegrationTest.targets")]
    [DeploymentItem("Aderant.Build.Common.targets")]
    public class GetAssembliesWithTraitsTests : BuildTaskTestBase {
        [TestMethod]
        public void GetAssembliesWithTraitsTest() {
            RunTarget("GetAssembliesWithTraits");
        }
    }
}