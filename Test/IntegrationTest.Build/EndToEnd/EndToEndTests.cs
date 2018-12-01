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

        public string DeploymentItemsDirectory {
            get { return Path.Combine(TestContext.DeploymentDirectory, "Resources", "Source"); }
        }

        [TestInitialize]
        public void TestInit() {
            AddFilesToNewGitRepository();
        }

        [TestMethod]
        [Description("The basic scenario. No changes - the build should reuse existing artifacts")]
        public void Build_tree_reuse_scenario() {
            using (var buildService = new TestBuildServiceHost(TestContext, DeploymentItemsDirectory)) {
                // Simulate first build
                RunTarget("EndToEnd", buildService.Properties);

                var context = buildService.GetContext();

                Assert.AreEqual(2, context.WrittenStateFiles.Count);
                Assert.IsTrue(context.WrittenStateFiles.All(File.Exists));
            }

            using (var buildService = new TestBuildServiceHost(TestContext, DeploymentItemsDirectory)) {
                buildService.PrepareForAnotherRun();

                // Run second build
                RunTarget("EndToEnd", buildService.Properties);

                var context = buildService.GetContext();

                foreach (string entry in context.WrittenStateFiles) {
                    if (entry.EndsWith(@"1\StateFile\buildstate.metadata")) {
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
            }
        }

        [TestMethod]
        public void When_project_is_deleted_build_still_completes() {
            using (var buildService = new TestBuildServiceHost(TestContext, DeploymentItemsDirectory)) {
                RunTarget("EndToEnd", buildService.Properties);

                Directory.Delete(Path.Combine(DeploymentItemsDirectory, "ModuleB", "Flob"), true);

                CleanWorkingDirectory();
                CommitChanges();
            }

            using (var buildService = new TestBuildServiceHost(TestContext, DeploymentItemsDirectory)) {
                buildService.PrepareForAnotherRun();
                CleanWorkingDirectory();

                var context = buildService.GetContext();

                Assert.IsNotNull(context.BuildStateMetadata);
                Assert.IsNotNull(context.WrittenStateFiles);
                Assert.AreNotEqual(0, context.BuildStateMetadata.BuildStateFiles.Count);

                RunTarget("EndToEnd", buildService.Properties);
            }
        }

        private void CleanWorkingDirectory() {
            RunCommand("& git clean -fdx");
        }

        private void CommitChanges() {
            RunCommand("& git add .");
            RunCommand("& git commit -m \"Add\"");
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

    internal class TestBuildServiceHost : IDisposable {
        private readonly string deploymentItemsDirectory;
        private readonly TestContext testContext;

        private BuildOperationContext context;

        private string endpoint;
        private Dictionary<string, string> properties;
        private BuildPipelineServiceClient client;
        private BuildPipelineServiceHost service;

        public TestBuildServiceHost(TestContext testContext, string deploymentItemsDirectory) {
            this.testContext = testContext;
            this.deploymentItemsDirectory = deploymentItemsDirectory;
        }

        public IDictionary<string, string> Properties {
            get {
                Initialize();
                return properties;
            }
        }

        public void Dispose() {
            try {
                client?.Dispose();
            } catch {

            }

            try {
                service?.Dispose();
            } catch {

            }
        }

        private void Initialize() {
            if (properties == null) {
                endpoint = testContext.TestName + "_" + Guid.NewGuid();

                this.properties = new Dictionary<string, string> {
                    { "BuildSystemInTestMode", bool.TrueString },
                    { "BuildScriptsDirectory", testContext.DeploymentDirectory + "\\" },
                    { "CompileBuildSystem", bool.FalseString },
                    { "ProductManifestPath", Path.Combine(deploymentItemsDirectory, "ExpertManifest.xml") },
                    { "SolutionRoot", Path.Combine(deploymentItemsDirectory) },
                    { "ArtifactStagingDirectory", $@"{Path.Combine(testContext.DeploymentDirectory, Guid.NewGuid().ToString())}\" },
                    { "PackageArtifacts", bool.TrueString },
                    { "XamlBuildDropLocation", "A" },
                    { "CopyToDropEnabled", bool.TrueString },
                    { "GetProduct", bool.FalseString },
                    { "PackageProduct", bool.TrueString },
                    { "RunTests", bool.FalseString },
                    { WellKnownProperties.ContextEndpoint, endpoint },
                };

                context = CreateContext(properties);

                StartService();
                service.Publish(context);

                this.client = new BuildPipelineServiceClient(service.ServerUri.AbsoluteUri);

                properties["PrimaryDropLocation"] = context.DropLocationInfo.PrimaryDropLocation;
                properties["BuildCacheLocation"] = context.DropLocationInfo.BuildCacheLocation;
            }
        }

        private void StartService() {
            if (service != null) {
                service.Dispose();
            }

            service = new BuildPipelineServiceHost();
            service.StartService(endpoint);
        }

        public BuildOperationContext GetContext() {
            return service.CurrentContext;
        }

        private BuildOperationContext CreateContext(Dictionary<string, string> props) {
            var ctx = new BuildOperationContext();
            ctx.DropLocationInfo.PrimaryDropLocation = Path.Combine(testContext.DeploymentDirectory, testContext.TestName, "_drop");
            ctx.DropLocationInfo.BuildCacheLocation = Path.Combine(testContext.DeploymentDirectory, testContext.TestName, "_cache");

            ctx.BuildMetadata = new BuildMetadata { BuildSourcesDirectory = deploymentItemsDirectory };

            ctx.SourceTreeMetadata = GetSourceTreeMetadata();
            ctx.BuildScriptsDirectory = props["BuildScriptsDirectory"];
            ctx.BuildSystemDirectory = Path.Combine(testContext.DeploymentDirectory, @"..\..\");

            return ctx;
        }

        public void PrepareForAnotherRun() {
            Initialize();

            context = CreateContext(properties);
            context.BuildMetadata.BuildId += 1;

            var buildStateMetadata = new ArtifactService(NullLogger.Default)
                .GetBuildStateMetadata(
                    context.SourceTreeMetadata.GetBuckets().Select(s => s.Id).ToArray(),
                    context.DropLocationInfo.BuildCacheLocation);

            Assert.AreNotEqual(0, buildStateMetadata.BuildStateFiles.Count);

            context.BuildStateMetadata = buildStateMetadata;
            
            service.Publish(context);
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var versionControl = new GitVersionControlService();
            return versionControl.GetMetadata(deploymentItemsDirectory, "", "");
        }
    }
}
