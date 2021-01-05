using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {
    [TestClass]
    [DeploymentItem("EndToEnd\\Resources\\", "EndToEndDeleteResources\\")]
    public class EndToEndTestsDelete : EndToEndTestsBase {
        protected override string DeploymentItemsDirectory =>
            // Square brackets bring gMSA parity to the desktop builds
            // PowerShell has many quirks with square brackets in paths so lets cause more issues locally to
            // avoid difficult to troubleshoot path issues.
            Path.Combine(TestContext.DeploymentDirectory, "EndToEndDeleteResources", "[0]", "Source");

        [TestInitialize]
        public void TestInit() {
            AddFilesToNewGitRepository();
        }

        /// <summary>
        /// Beware: This test affects global state as it removes files from the deployment directory
        /// </summary>
        [TestMethod]
        public void When_project_is_deleted_build_still_completes() {
            using (var buildService = new TestBuildServiceHost(DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                RunTarget("EndToEnd", buildService.Properties);

                Directory.Delete(Path.Combine(DeploymentItemsDirectory, "ModuleB", "Flob"), true);

                CleanWorkingDirectory();
                CommitChanges();
            }

            using (var buildService = new TestBuildServiceHost(DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                buildService.PrepareForAnotherRun();
                CleanWorkingDirectory();

                var context = buildService.GetContext();

                Assert.IsNotNull(context.BuildStateMetadata);
                Assert.IsNotNull(context.WrittenStateFiles);
                Assert.AreNotEqual(0, context.BuildStateMetadata.BuildStateFiles.Count);

                RunTarget("EndToEnd", buildService.Properties);
            }
        }
    }
}
