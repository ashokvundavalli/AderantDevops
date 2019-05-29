using System.Linq;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class PowerShellPipelineExecutorTests {

        [TestMethod]
        public void Can_get_result_from_script() {
            var executor = new PowerShellPipelineExecutor();

            executor.RunScript(new[] { "Get-FileHash -LiteralPath C:\\Windows\\System32\\notepad.exe -Algorithm SHA1 -Verbose | Select-Object -ExpandProperty Hash" }, null);

            var executorResult = executor.Result;

            Assert.IsNotNull(executorResult);
        }

        [TestMethod]
        [ExpectedException(typeof(Microsoft.PowerShell.Commands.WriteErrorException))]
        public void Fail_result_from_script() {
            var executor = new PowerShellPipelineExecutor();

            executor.RunScript(new[] { "Write-Error 'Oh no'" }, null);
        }

        [TestMethod]
        public void Stream_capture() {
            var executor = new PowerShellPipelineExecutor();

            int[] calls = new int[5];

            executor.Info += (sender, record) => { calls[0] += 1; };

            executor.Warning += (sender, record) => { calls[1] += 1; };

            executor.Debug += (sender, record) => { calls[2] += 1; };

            executor.Verbose += (sender, record) => { calls[3] += 1; };

            executor.DataReady += (sender, collection) => { calls[4] += 1; };

            executor.RunScript(
                new[] {
                    @"
$VerbosePreference = 'Continue'
Write-Verbose 'Verbose'

Write-Information 'Information'

Write-Output 'Output'

Write-Warning 'Warning'

Write-Host 'Host'

$DebugPreference = 'Continue'
Write-Debug 'Verbose'"
                },
                null);

            Assert.AreEqual(6, calls.Sum());
        }

        [TestMethod]
        [ExpectedException(typeof(Microsoft.PowerShell.Commands.WriteErrorException))]
        public void Error_capture() {
            var executor = new PowerShellPipelineExecutor();

            object errors = null;
            executor.ErrorReady += (sender, collection) => { errors = collection; };

            executor.RunScript(
                new[] {
                    @"
Write-Error 'Error'
"
                },
                null);
        }
    }
}
