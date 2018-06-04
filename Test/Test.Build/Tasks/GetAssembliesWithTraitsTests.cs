using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class GetAssembliesWithTraitsTests : BuildTaskTestBase {
        [TestMethod]
        public void GetAssembliesWithTraitsTest() {
            RunTarget("GetAssembliesWithTraits");
        }
    }
}