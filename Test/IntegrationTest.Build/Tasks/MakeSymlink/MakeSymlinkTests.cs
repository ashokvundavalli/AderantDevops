using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.MakeSymlink {
    [TestClass]
    public class MakeSymlinkTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void MakeSymlinkTest() {
            RunTarget(nameof(Aderant.Build.Tasks.MakeSymlink));
        }
    }
}
