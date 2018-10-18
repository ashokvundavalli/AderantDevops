using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.PowerShellScript {
    [TestClass]
    public class PowerShellScriptTests : MSBuildIntegrationTestBase {

        [TestMethod]
        public void PowerShellScript_runs_without_exception() {
            RunTarget("PowerShellScript");

            Assert.IsFalse(Logger.HasRaisedErrors);

            CollectionAssert.Contains(LogFile, "    AAAA\r\n", "Write-Host message was not captured");
            CollectionAssert.Contains(LogFile, "    BBBB\r\n", "Write-Information message was not captured");
            Assert.IsNotNull(LogFile.Find(s => s.Contains("warning : DDDD")));
        }
    }
}
