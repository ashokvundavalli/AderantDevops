using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GetAssembliesWithTraits {
    [TestClass]
    public class GetAssembliesWithTraitsTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void GetAssembliesWithTraitsTest() {
            RunTarget("GetAssembliesWithTraits");
        }
    }
}