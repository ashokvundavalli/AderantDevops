using System;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.RunAnalyzer {
    [TestClass]
    [DeploymentItem("Aderant.Build.Analyzer.dll")]
    public class RunAnalyzerTests : BuildTaskTestBase {

        [ClassInitialize]
        public static void ClasInitialize(TestContext context) {
            var unused = typeof(CodeQualitySystemDiagnosticsRule).FullName;
        }
       
        [TestMethod]
        public void RunAnalyzer_CiOnlyRules() {
            // If the test is being run in an environment that lacks a Build ID, exit early.
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILD_BUILDID"))) {
                return;
            }

            bool exceptionCaught = false;

            // Run the analyzer.
            // When an error occurs, it is logged.
            // The logger is then asserted upon, and raises an assert failure if any errors were logged.
            // Errors are expected if the rule runs correctly.
            try {
                RunTarget("RunAnalyzer");
            } catch (AssertFailedException) {
                exceptionCaught = true;
            }

            Assert.IsTrue(exceptionCaught);
        }
    }
}
