using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Management.Automation;
using IntegrationTest.Build.Helpers;

namespace IntegrationTest.Build {
    [TestClass]
    [DeploymentItem(@"Resources\SampleTestRun.trx", "SlowTestReporter")]
    public class SlowTestReporterTests : MSBuildIntegrationTestBase {

        [TestMethod]
        [Description("Checks that the SlowTestReporter runs and finds the expected line on the trx resource.")]
        public void SlowTestReporterProcessesTrxAndProducesReport() {
            // Arrange
            var messageToSearchFor = new MessageLocator("One test found that exceeded the maximum test duration", typeof(InformationRecord));
            var powerShellHelper = new PowerShellHelper(new List<MessageLocator>{messageToSearchFor});
            var scriptPath = Path.Combine(TestContext.DeploymentDirectory, "Build", "Testing", "SlowTestReporter.ps1");
            var sampleTestRunDir = Path.Combine(TestContext.DeploymentDirectory, "SlowTestReporter");

            // Act
            powerShellHelper.RunCommand($"& '{scriptPath}' -path '{sampleTestRunDir}'"
                , TestContext
                , TestContext.DeploymentDirectory);

            // Assert
            Assert.IsTrue(File.Exists(Path.Combine(sampleTestRunDir, "SlowTestReport.csv ")), "Did not find a report csv in the expected directory.");
            Assert.IsTrue(powerShellHelper.FoundAllMessages, "Failed to find a log message indicating that a test was found that exceeded the given timeout.");
        }
    }
}
