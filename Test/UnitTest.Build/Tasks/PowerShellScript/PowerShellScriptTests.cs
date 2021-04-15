using System;
using System.Management.Automation;
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
        public void TransformMessageNullTest() {
            WarningRecord warningRecord = new WarningRecord(null);

            Assert.AreEqual(null, warningRecord.ToString());

            string result = Aderant.Build.Tasks.PowerShell.PowerShellScript.TransformMessage(warningRecord);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void TransformMessageNonNullTest() {
            WarningRecord warningRecord = new WarningRecord("test");

            Assert.AreEqual("test", warningRecord.ToString());

            string result = Aderant.Build.Tasks.PowerShell.PowerShellScript.TransformMessage(warningRecord);

            Assert.AreEqual("test", result);
        }
    }
}