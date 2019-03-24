using IntegrationTest.Build.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {
    [TestClass]
    [DeploymentItem(@"TestDeployment\x86\", "x86")]
    [DeploymentItem(@"TestDeployment\x64\", "x64")]
    public abstract class GitVersionControlTestBase {

        public virtual TestContext TestContext { get; set; }

        public string RepositoryPath { get; set; }

        protected static string RunPowerShellInIsolatedDirectory(TestContext context, string script) {
            return PowerShellHelper.RunCommand(script, context, null);
        }

        protected static string RunPowerShellInDirectory(TestContext testContext, string script, string repositoryPath) {
            return PowerShellHelper.RunCommand(script, testContext, repositoryPath);
        }
    }
}