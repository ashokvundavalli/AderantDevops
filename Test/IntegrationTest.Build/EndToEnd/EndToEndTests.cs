using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {

    [TestClass]
    [DeploymentItem("EndToEnd\\Resources", "Resources")]
    public class EndToEndTests : MSBuildIntegrationTestBase {
        private BuildOperationContext context;
        private string contextFile;
        private Dictionary<string, string> properties;

        public string DeploymentItemsDirectory {
            get { return Path.Combine(TestContext.DeploymentDirectory, "Resources", "Source"); }
        }

        [TestInitialize]
        public void TestInitialize() {
            AddFilesToNewGitRepository();

            this.properties = new Dictionary<string, string> {
                { "BuildSystemInTestMode", bool.TrueString },
                { "BuildScriptsDirectory", TestContext.DeploymentDirectory + "\\" },
                { "CompileBuildSystem", bool.FalseString },
                { "ProductManifestPath", Path.Combine(DeploymentItemsDirectory, "ExpertManifest.xml") },
                { "SolutionRoot", Path.Combine(DeploymentItemsDirectory) },
                { WellKnownProperties.ContextFileName, TestContext.TestName },
            };

            this.context = CreateContext(properties);
            this.contextFile = context.Publish(properties[WellKnownProperties.ContextFileName]);
        }

        [TestMethod]
        [Description("The basic scenario. No changes - the build should reuse existing artifacts")]
        public void Build_tree_reuse_scenario() {
            //base.DetailedSummary = false;
            //base.LoggerVerbosity = LoggerVerbosity.Normal;

            // Simulate first build
            RunTarget("EndToEnd", properties);

            var logFile = base.LogFile;

            PrepareForAnotherRun(BucketId.Current);

            // Run second build - 
            RunTarget("EndToEnd", properties);
            var logFile1 = base.LogFile;

            WriteLogFile(@"C:\Temp\lf.log", logFile);
            WriteLogFile(@"C:\Temp\lf1.log", logFile1);
        }

        [TestMethod]
        public void Project_is_deleted() {
            RunTarget("EndToEnd", properties);

            Directory.Delete(Path.Combine(DeploymentItemsDirectory, "ModuleB", "Flob"), true);
            CleanWorkingDirectory();
            CommitChanges();
            PrepareForAnotherRun(BucketId.Previous); // Grab the previous tree state as the basis

            Assert.IsNotNull(context.BuildStateMetadata);
            Assert.AreNotEqual(0, context.BuildStateMetadata.BuildStateFiles.Count);

            RunTarget("EndToEnd", properties);
        }

        private void PrepareForAnotherRun(string bucket) {
            context = contextFile.GetBuildOperationContext();
            context = CreateContext(properties);
            context.BuildMetadata.BuildId += 1;

            var buildStateMetadata = new ArtifactService(NullLogger.Default).GetBuildStateMetadata(
                new[] {
                    context.SourceTreeMetadata.GetBucket(bucket).Id
                }, context.PrimaryDropLocation);

            context.BuildStateMetadata = buildStateMetadata;
            context.Publish(properties[WellKnownProperties.ContextFileName]);

            CleanWorkingDirectory();
        }

        private void CleanWorkingDirectory() {
            RunCommand("& git clean -fdx");
        }

        private void CommitChanges() {
            RunCommand("& git add .");
            RunCommand("& git commit -m \"Add\"");
        }

        private BuildOperationContext CreateContext(Dictionary<string, string> props) {
            var ctx = new BuildOperationContext();
            ctx.PrimaryDropLocation = Path.Combine(TestContext.DeploymentDirectory, "_drop");
            ctx.BuildMetadata = new BuildMetadata();
            ctx.BuildMetadata.BuildSourcesDirectory = DeploymentItemsDirectory;
            ctx.SourceTreeMetadata = GetSourceTreeMetadata();
            ctx.BuildScriptsDirectory = props["BuildScriptsDirectory"];
            ctx.BuildSystemDirectory = Path.Combine(TestContext.DeploymentDirectory, @"..\..\");

            return ctx;
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var versionControl = new GitVersionControl();
            return versionControl.GetMetadata(DeploymentItemsDirectory, "", "");
        }

        private void AddFilesToNewGitRepository() {
            RunCommand(Resources.CreateRepo);
        }

        private void RunCommand(string command) {
            using (var ps = PowerShell.Create()) {
                ps.AddScript($"cd {DeploymentItemsDirectory.Quote()}");
                ps.AddScript(command);
                var results = ps.Invoke();

                foreach (var item in results) {
                    TestContext.WriteLine(item.ToString());
                }
            }
        }
    }
}
