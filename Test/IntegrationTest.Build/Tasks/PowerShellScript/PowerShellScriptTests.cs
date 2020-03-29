using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.PowerShellScript {
    [TestClass]
    public class PowerShellScriptTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void PowerShellScript_runs_without_exception() {
            RunTarget("PowerShellScript");

            Assert.IsFalse(Logger.HasRaisedErrors);

            CollectionAssert.Contains(LogLines, "    AAAA\r\n", "Write-Host message was not captured");
            CollectionAssert.Contains(LogLines, "    BBBB\r\n", "Write-Output message was not captured");
            CollectionAssert.Contains(LogLines, "    EEEE\r\n", "Write-Information message was not captured");
            Assert.IsNotNull(LogLines.Find(s => s.Contains("warning : DDDD")));
        }


        [TestMethod]
        public void PowerShellScript_accepts_script_file_arguments() {
            RunTarget("PowerShellScriptFile_with_args");

            Assert.AreEqual("TheValue", GetSingleTargetOutputValue());
        }


        [TestMethod]
        public void PowerShellScript_accepts_ScriptBlock_arguments() {
            RunTarget("PowerShellScriptBlock_with_args");

            Assert.IsFalse(Logger.HasRaisedErrors);
        }

        [TestMethod]
        public void PowerShellScriptBlock_calling_ScriptBlock_with_args() {
            RunTarget("PowerShellScriptBlock_calling_ScriptBlock_with_args");
        }


        [TestMethod]
        public void PowerShellScriptBlock_with_named_args() {
            RunTarget("PowerShellScriptBlock_with_named_args");

            var result = GetTargetOutputCollection();

            CollectionAssert.Contains(result, "TheValue3");
            CollectionAssert.Contains(result, "TheValue2");
        }

        private string[] GetTargetOutputCollection() {
            return GetItems().Select(s => s.EvaluatedInclude).ToArray();
        }

        private string GetSingleTargetOutputValue() {
            return GetItems().Single().EvaluatedInclude;
        }

        private ICollection<ProjectItemInstance> GetItems() {
            return Result.ProjectStateAfterBuild.GetItems("TargetResult");
        }
    }
}