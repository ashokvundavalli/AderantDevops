using System;
using System.IO;
using System.Linq;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {
    [TestClass]
    [DeploymentItem("EndToEnd\\Resources\\", "Resources\\")]
    public class EndToEndTests : EndToEndTestsBase {

        [TestInitialize]
        public void TestInit() {
            AddFilesToNewGitRepository();
        }

        protected override string DeploymentItemsDirectory =>
            // Square brackets bring gMSA parity to the desktop builds
            // PowerShell has many quirks with square brackets in paths so lets cause more issues locally to
            // avoid difficult to troubleshoot path issues.
            Path.Combine(TestContext.DeploymentDirectory, "Resources", "[0]", "Source");

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
                    if (entry.EndsWith("buildstate.metadata")) {
                        BuildStateFile stateFile;
                        using (var fs = new FileStream(entry, FileMode.Open)) {
                            stateFile = StateFileBase.DeserializeCache<BuildStateFile>(fs);
                        }

                        if (stateFile.BucketId.Tag == "ModuleB") {
                            Assert.AreEqual(1, stateFile.Artifacts.Keys.Count);
                        } else {
                            Assert.IsTrue(stateFile.Artifacts.ContainsKey("ModuleA"));

                            var manifest = stateFile.Artifacts["ModuleA"];
                            Assert.IsTrue(manifest.Any(m => string.Equals(m.Id, "ModuleA", StringComparison.OrdinalIgnoreCase)));
                            Assert.IsTrue(manifest.Any(m => string.Equals(m.Id, "Tests.ModuleA", StringComparison.OrdinalIgnoreCase)));

                            Assert.IsNotNull(stateFile.Outputs);
                        }

                        Assert.IsNotNull(stateFile.BuildConfiguration);
                        Assert.IsNotNull(stateFile.BuildConfiguration["Flavor"]);

                        Assert.IsNotNull(stateFile.PackageHash);
                        Assert.IsNotNull(stateFile.PackageGroups);
                        Assert.AreEqual(2, stateFile.PackageGroups.Count);
                    }
                }
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

                Assert.AreEqual(Resources.my_custom_runsettings, File.ReadAllText(propertyValue));
            }
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

                Assert.AreEqual(Resources.Expected_run_settings, File.ReadAllText(propertyValue));
            }
        }

        private string RunTestTargetAndGetOutputs(TestBuildServiceHost buildService) {
            RunTarget("CollectProjectsInBuild", buildService.Properties);
            var propertyValue = base.Result.ProjectStateAfterBuild.GetPropertyValue("RunSettingsFile");

            Assert.IsNotNull(propertyValue);
            return propertyValue;
        }
    }
}
