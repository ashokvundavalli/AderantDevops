using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Aderant.Build;
using Aderant.Build.Packaging;
using Aderant.Build.VersionControl;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {

    [TestClass]
    [DeploymentItem("EndToEnd\\Resources", "Resources")]
    public class EndToEndTests : MSBuildIntegrationTestBase {

        [TestMethod]
        public void Builds_reuse_cached_outputs() {
            base.DetailedSummary = false;
            base.LoggerVerbosity = LoggerVerbosity.Normal;

            AddFilesToNewGitRepository(DeploymentItemsDirectory);

            var properties = new Dictionary<string, string> {
                { "BuildSystemInTestMode", bool.TrueString },
                { "BuildScriptsDirectory", TestContext.DeploymentDirectory + "\\" },
                { "CompileBuildSystem", bool.FalseString },
                { "ProductManifestPath", Path.Combine(DeploymentItemsDirectory, "ExpertManifest.xml") },
                { "SolutionRoot", Path.Combine(DeploymentItemsDirectory) },
                { WellKnownProperties.ContextFileName, TestContext.TestName },
            };

            var context = CreateContext(properties);
            var contextFile = context.Publish(properties[WellKnownProperties.ContextFileName]);

            // Simulate first build
            RunTarget("EndToEnd", properties);

            var logFile = base.LogFile;

            context = contextFile.GetBuildOperationContext();

            // Simulate first build
            context = CreateContext(properties);
            context.BuildMetadata.BuildId = 1;

            var buildStateMetadata = new ArtifactService(Aderant.Build.Logging.NullLogger.Default).GetBuildStateMetadata(new[] { context.SourceTreeMetadata.GetBucket(BucketId.Current).Id }, context.PrimaryDropLocation);
            context.BuildStateMetadata = buildStateMetadata;
            context.Publish(properties[WellKnownProperties.ContextFileName]);

            // Run second build
            RunTarget("EndToEnd", properties);
            var logFile1 = base.LogFile;

            WriteLogFile(@"C:\Temp\lf.log", logFile);
            WriteLogFile(@"C:\Temp\lf1.log", logFile1);
        }

        private BuildOperationContext CreateContext(Dictionary<string, string> properties) {
            var context = new BuildOperationContext();
            context.PrimaryDropLocation = Path.Combine(TestContext.DeploymentDirectory, "_drop");
            context.BuildMetadata = new BuildMetadata();
            context.SourceTreeMetadata = GetSourceTreeMetadata();


            context.BuildScriptsDirectory = properties["BuildScriptsDirectory"];
            context.BuildSystemDirectory = Path.Combine(TestContext.DeploymentDirectory, @"..\..\");

            return context;
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var versionControl = new GitVersionControl();
            return versionControl.GetMetadata(DeploymentItemsDirectory, "", "");
        }

        private void AddFilesToNewGitRepository(string testContextDeploymentDirectory) {
            using (var ps = PowerShell.Create()) {
                ps.AddScript($"cd {testContextDeploymentDirectory.Quote()}");
                ps.AddScript(Resources.CreateRepo);
                var results = ps.Invoke();

                foreach (var item in results) {
                    TestContext.WriteLine(item.ToString());
                }
            }
        }

        public string DeploymentItemsDirectory {
            get {
                return Path.Combine(TestContext.DeploymentDirectory, "Resources", "Source");
            }
        }
    }
}
