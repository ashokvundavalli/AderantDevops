using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {

    [TestClass]
    [DeploymentItem("EndToEnd\\Resources", "Resources")]
    public class EndToEndTests : MSBuildIntegrationTestBase {
        private BuildOperationContext context;
        private string contextFile;
        private Dictionary<string, string> properties;
        private BuildPipelineServiceFactory service;
        private IBuildPipelineServiceContract proxy;

        public string DeploymentItemsDirectory {
            get { return Path.Combine(TestContext.DeploymentDirectory, "Resources", "Source"); }
        }

        [TestInitialize]
        public void TestInitialize() {
            AddFilesToNewGitRepository();

            contextFile = TestContext.TestName + "_" + Guid.NewGuid();

            this.properties = new Dictionary<string, string> {
                { "BuildSystemInTestMode", bool.TrueString },
                { "BuildScriptsDirectory", TestContext.DeploymentDirectory + "\\" },
                { "CompileBuildSystem", bool.FalseString },
                { "ProductManifestPath", Path.Combine(DeploymentItemsDirectory, "ExpertManifest.xml") },
                { "SolutionRoot", Path.Combine(DeploymentItemsDirectory) },
                { "PackageArtifacts", bool.TrueString },
                { "XamlBuildDropLocation", "A" },
                { WellKnownProperties.ContextFileName, contextFile },
            };

            context = CreateContext(properties);

            service = new BuildPipelineServiceFactory();
            service.StartListener(contextFile);
            this.proxy = BuildPipelineServiceFactory.CreateProxy(contextFile);
            service.Publish(context);
        }

        [TestMethod]
        [Description("The basic scenario. No changes - the build should reuse existing artifacts")]
        public void Build_tree_reuse_scenario() {
            // Simulate first build
            RunTarget("EndToEnd", properties);

            context = proxy.GetContext();
            Assert.AreEqual(2, Directory.GetFileSystemEntries(context.Drops.PrimaryDropLocation, "buildstate.metadata", SearchOption.AllDirectories).Length);

            var logFile = base.LogFile;

            PrepareForAnotherRun();

            // Run second build
            RunTarget("EndToEnd", properties);
            foreach (string entry in Directory.GetFileSystemEntries(context.Drops.PrimaryDropLocation, "buildstate.metadata", SearchOption.AllDirectories)) {
                if (entry.EndsWith(@"1\buildstate.metadata")) {
                    var stateFile = StateFileBase.DeserializeCache<BuildStateFile>(new FileStream(entry, FileMode.Open));

                    if (stateFile.BucketId.Tag == "ModuleB") {
                        Assert.AreEqual(1, stateFile.Artifacts.Keys.Count);
                    } else {
                        Assert.IsTrue(stateFile.Artifacts.ContainsKey("ModuleA"));

                        var manifest = stateFile.Artifacts["ModuleA"];
                        Assert.IsTrue(manifest.Any(m => m.Id == "ModuleA"));
                        Assert.IsTrue(manifest.Any(m => m.Id == "Tests.ModuleA"));

                        Assert.IsNotNull(stateFile.Outputs);
                    }
                }
            }

            var logFile1 = base.LogFile;

            WriteLogFile(@"C:\Temp\lf.log", logFile);
            WriteLogFile(@"C:\Temp\lf1.log", logFile1);
        }

        [TestMethod]
        public void When_project_is_deleted_build_still_completes() {
            RunTarget("EndToEnd", properties);

            Directory.Delete(Path.Combine(DeploymentItemsDirectory, "ModuleB", "Flob"), true);
            CleanWorkingDirectory();
            CommitChanges();
            PrepareForAnotherRun();

            Assert.IsNotNull(context.BuildStateMetadata);
            Assert.AreNotEqual(0, context.BuildStateMetadata.BuildStateFiles.Count);

            RunTarget("EndToEnd", properties);
        }

        private void PrepareForAnotherRun() {
            context = proxy.GetContext();
            context = CreateContext(properties);
            context.BuildMetadata.BuildId += 1;

            var buildStateMetadata = new ArtifactService(NullLogger.Default)
                .GetBuildStateMetadata(
                    context.SourceTreeMetadata.GetBuckets().Select(s => s.Id).ToArray(),
                    context.Drops.PrimaryDropLocation);

            context.BuildStateMetadata = buildStateMetadata;
            service.Publish(context);

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
            ctx.Drops.PrimaryDropLocation = Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, "_drop");
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
