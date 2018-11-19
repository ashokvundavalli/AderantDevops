using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.CheckForDoubleWrites {
    [TestClass]
    public class CheckForDoubleWritesTests : MSBuildIntegrationTestBase {

        [TestMethod]
        public void Build_fails_on_double_write() {
            BuildMustSucceed = false;

            RunTarget("CheckForDoubleWritesTests");

            Assert.IsTrue(this.LogFile.Any(s => s.Contains("Double write")));
        }
    }
}