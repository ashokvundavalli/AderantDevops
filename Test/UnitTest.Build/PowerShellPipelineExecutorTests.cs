using System.Threading.Tasks;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class PowerShellPipelineExecutorTests {

        [TestMethod]
        public async Task Can_get_result_from_script() {
            var executor = new PowerShellPipelineExecutor();

            await executor.RunScript("Get-FileHash -LiteralPath C:\\Windows\\System32\\notepad.exe -Algorithm SHA1 -Verbose | Select-Object -ExpandProperty Hash");

            var executorResult = executor.Result;

            Assert.IsNotNull(executorResult);
        }
    }
}
