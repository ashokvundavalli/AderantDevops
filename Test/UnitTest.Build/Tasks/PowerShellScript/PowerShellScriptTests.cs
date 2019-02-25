using System;
using Aderant.Build.PipelineService;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Tasks.PowerShellScript {
    [TestClass]
    public class PowerShellScriptTests {

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void No_scripts_results_in_error() {
            var script = new Aderant.Build.Tasks.PowerShell.PowerShellScript();
            script.BuildEngine = new Mock<IBuildEngine>().As<IBuildEngine3>().As<IBuildEngine4>().Object;

            script.Execute();
        }

        [TestMethod]
        public void ScriptResource_sets_script_block() {
            var script = new TestPowerShellScript();
            script.BuildEngine = new Mock<IBuildEngine>().As<IBuildEngine3>().As<IBuildEngine4>().Object;
            script.ScriptResource = "SetBuildTags";

            script.Execute();

            Assert.IsNotNull(script.ScriptBlock);
        }
    }

    public class TestPowerShellScript : Aderant.Build.Tasks.PowerShell.PowerShellScript {
        internal override IBuildPipelineService GetProxy() {
            return null;
        }
    }
}