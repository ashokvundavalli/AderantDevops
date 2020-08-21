using System.IO;
using System.Linq;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using IntegrationTest.Build.Helpers;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {
    [TestClass]
    [DeploymentItem("EndToEnd\\Resources\\", "Resources\\")]
    public class EndToEndTests : MSBuildIntegrationTestBase {
        public string DeploymentItemsDirectory {
            // Square brackets bring gMSA parity to the desktop builds
            // PowerShell has many quirks with square brackets in paths so lets cause more issues locally to
            // avoid difficult to troubleshoot path issues.
            get { return Path.Combine(TestContext.DeploymentDirectory, "Resources", "[0]", "Source"); }
        }

        [TestInitialize]
        public void TestInit() {
            AddFilesToNewGitRepository();
        }

        [TestMethod]
        [Description("The basic scenario. No changes - the build should reuse existing artifacts")]
        public void Build_tree_reuse_scenario() {
            using (var buildService = new TestBuildServiceHost(base.DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                // Simulate first build
                RunTarget("EndToEnd", buildService.Properties);

                var context = buildService.GetContext();

                Assert.IsNotNull(context.BuildRoot);
                Assert.AreEqual(2, context.WrittenStateFiles.Count);
                Assert.IsTrue(context.WrittenStateFiles.All(File.Exists));
            }

            using (var buildService = new TestBuildServiceHost(base.DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                buildService.PrepareForAnotherRun();

                // Run second build
                RunTarget("EndToEnd", buildService.Properties);

                var context = buildService.GetContext();

                foreach (string entry in context.WrittenStateFiles) {
                    if (entry.EndsWith(@"1\StateFile\buildstate.metadata")) {
                        BuildStateFile stateFile;
                        using (var fs = new FileStream(entry, FileMode.Open)) {
                            stateFile = StateFileBase.DeserializeCache<BuildStateFile>(fs);
                        }

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

        /// <summary>
        /// Beware: This test affects global state as it removes files from the deployment directory
        /// </summary>
        [TestMethod]
        public void When_project_is_deleted_build_still_completes() {
            using (var buildService = new TestBuildServiceHost(base.DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                RunTarget("EndToEnd", buildService.Properties);

                Directory.Delete(Path.Combine(DeploymentItemsDirectory, "ModuleB", "Flob"), true);

                CleanWorkingDirectory();
                CommitChanges();
            }

            using (var buildService = new TestBuildServiceHost(base.DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                buildService.PrepareForAnotherRun();
                CleanWorkingDirectory();

                var context = buildService.GetContext();

                Assert.IsNotNull(context.BuildStateMetadata);
                Assert.IsNotNull(context.WrittenStateFiles);
                Assert.AreNotEqual(0, context.BuildStateMetadata.BuildStateFiles.Count);

                RunTarget("EndToEnd", buildService.Properties);
            }
        }

        [TestMethod]
        public void When_custom_runsettings_file_present_it_is_used_by_the_build() {
            using (var buildService = new TestBuildServiceHost(base.DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                buildService.Initialize();

                OnDiskProjectInfo trackedProject;
                buildService.Client.TrackProject(trackedProject = new OnDiskProjectInfo {
                    SolutionRoot = Path.Combine(DeploymentItemsDirectory, "ModuleA"),
                    FullPath = Path.Combine(DeploymentItemsDirectory, @"ModuleA\Bar\Bar.csproj"),
                    OutputPath = Path.Combine(DeploymentItemsDirectory, @"ModuleA\Bar"),
                });

                Assert.IsTrue(File.Exists(trackedProject.FullPath));

                var propertyValue = RunTestTargetAndGetOutputs(buildService);

                Assert.AreEqual(File.ReadAllText(propertyValue), Resources.my_custom_runsettings);
            }
        }

        private string RunTestTargetAndGetOutputs(TestBuildServiceHost buildService) {
            RunTarget("CollectProjectsInBuild", buildService.Properties);
            var values = base.Result.ProjectStateAfterBuild.GetPropertyValue("RunSettingsFile");

            Assert.IsNotNull(values);
            
     
            return values;
        }

        [TestMethod]
        public void When_custom_runsettings_item_group_present_it_is_used_by_the_build() {
            using (var buildService = new TestBuildServiceHost(base.DisableInProcNode, TestContext, DeploymentItemsDirectory)) {
                buildService.Initialize();

                OnDiskProjectInfo trackedProject;
                buildService.Client.TrackProject(trackedProject = new OnDiskProjectInfo {
                    SolutionRoot = Path.Combine(DeploymentItemsDirectory, "ModuleB"),
                    FullPath = Path.Combine(DeploymentItemsDirectory, @"ModuleB\Gaz\Gaz.csproj"),
                    OutputPath = Path.Combine(DeploymentItemsDirectory, @"ModuleB\Gaz"),
                });

                Assert.IsTrue(File.Exists(trackedProject.FullPath));

                var propertyValue = RunTestTargetAndGetOutputs(buildService);

                Assert.AreEqual(File.ReadAllText(propertyValue), Resources.Expected_run_settings);
            }
        }

        private void CleanWorkingDirectory() {
            PowerShellHelper.RunCommand("& git clean -fdx", TestContext, DeploymentItemsDirectory);
        }

        private void CommitChanges() {
            PowerShellHelper.RunCommand("& git add .", TestContext, DeploymentItemsDirectory);
            PowerShellHelper.RunCommand("& git commit -m \"Add\"", TestContext, DeploymentItemsDirectory);
        }

        private void AddFilesToNewGitRepository() {
            PowerShellHelper.RunCommand(Resources.CreateRepo, TestContext, DeploymentItemsDirectory);
        }
    }
}