using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using Aderant.Build;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {

    [TestClass]
    [DeploymentItem("EndToEnd\\Resources\\", "Resources\\")]
    public class EndToEndTests : MSBuildIntegrationTestBase {

        public string DeploymentItemsDirectory {
            get { return Path.Combine(TestContext.DeploymentDirectory, "Resources", "Source"); }
        }

        [TestInitialize]
        public void TestInit() {
            AddFilesToNewGitRepository();
            Assert.IsTrue(Directory.Exists(DeploymentItemsDirectory));
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

    internal class PowerShellHelper {

        public static void RunCommand(string command, TestContext context, string deploymentItemsDirectory = null) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("$InformationPreference = 'Continue'");
            sb.AppendLine("$ErrorActionPreference = 'Stop'");

            Dictionary<string, object> variables = null;
            if (deploymentItemsDirectory != null) {
                variables = new Dictionary<string, object>();
                variables.Add("DeploymentItemsDirectory", deploymentItemsDirectory);
                sb.AppendLine($"Set-Location {deploymentItemsDirectory.Quote()}");
            }

            sb.AppendLine("Write-Information $PSScriptRoot");
            sb.AppendLine(command);

            var executor = new PowerShellPipelineExecutor();

            bool errors = false;

            EventHandler<ICollection<PSObject>> dataReady = (sender, objects) => {
                foreach (var psObject in objects) {
                    context.WriteLine(psObject.ToString());
                }
            };

            EventHandler<ICollection<object>> errorReady = (sender, objects) => {
                foreach (var psObject in objects) {
                    context.WriteLine(psObject.ToString());
                }

                errors = true;
            };

            EventHandler<InformationRecord> info = (sender, objects) => { context.WriteLine(objects.ToString()); };
            EventHandler<VerboseRecord> verbose = (sender, objects) => { context.WriteLine(objects.ToString()); };
            EventHandler<WarningRecord> warning = (sender, objects) => { context.WriteLine(objects.ToString()); };
            EventHandler<DebugRecord> debug = (sender, objects) => { context.WriteLine(objects.ToString()); };

            executor.DataReady += dataReady;
            executor.ErrorReady += errorReady;
            executor.Info += info;
            executor.Verbose += verbose;
            executor.Warning += warning;
            executor.Debug += debug;

            executor.RunScript(
                new[] {
                    command
                },
                variables,
                CancellationToken.None);

            executor.DataReady -= dataReady;
            executor.ErrorReady -= errorReady;
            executor.Info -= info;
            executor.Verbose -= verbose;
            executor.Warning -= warning;
            executor.Debug -= debug;

            if (errors) {
                Assert.Fail("During script execution an error occurred. Test failed.");
            }
        }
    }
}