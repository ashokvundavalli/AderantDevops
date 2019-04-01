using System;
using System.IO;
using IntegrationTest.Build.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {
    [TestClass]
    [DeploymentItem(MSBuildIntegrationTestBase.TestDeployment)]
    public abstract class GitVersionControlTestBase {

        public virtual TestContext TestContext { get; set; }

        public string RepositoryPath { get; set; }

        public string Script { get; set; } = Resources.CreateRepo;

        [TestInitialize]
        public virtual void TestInitialize() {
            PowerShellHelper.AssertCurrentDirectory();

            if (RepositoryPath == null) {
                // Square brackets bring gMSA parity to the desktop builds
                // PowerShell has many quirks with square brackets in paths so lets cause more issues locally to
                // avoid difficult to troubleshoot path issues.
                var path = Path.Combine(TestContext.DeploymentDirectory, "[" + DateTime.UtcNow.ToFileTimeUtc() + "]");
                RepositoryPath = RunPowerShellInDirectory(TestContext, Script, path);
            }

            Assert.IsNotNull(RepositoryPath);
        }

        protected static string RunPowerShellInIsolatedDirectory(TestContext context, string script) {
            return PowerShellHelper.RunCommand(script, context, null);
        }

        protected static string RunPowerShellInDirectory(TestContext testContext, string script, string repositoryPath) {
            return PowerShellHelper.RunCommand(script, testContext, repositoryPath);
        }
    }
}