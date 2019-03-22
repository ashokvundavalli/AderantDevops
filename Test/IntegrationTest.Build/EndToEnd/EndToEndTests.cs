﻿using System.Collections.Generic;
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
            Assert.IsTrue(Directory.Exists(DeploymentItemsDirectory));

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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("$InformationPreference = 'Continue'");
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$DeploymentItemsDirectory = " + DeploymentItemsDirectory.Quote());
            sb.AppendLine($"Set-Location {DeploymentItemsDirectory.Quote()}");
            sb.AppendLine(command);

            var executor = new PowerShellPipelineExecutor();

            executor.DataReady += OnExecutorOnDataReady;
            executor.Debug += OnExecutorOnDebug;
            executor.Info += OnExecutorOnInfo;
            executor.Verbose += OnExecutorOnVerbose;
            executor.ErrorReady += OnExecutorOnErrorReady;

            executor.RunScript(
                new[] {
                    sb.ToString()
                },
                null,
                CancellationToken.None);

            executor.DataReady -= OnExecutorOnDataReady;
            executor.Debug -= OnExecutorOnDebug;
            executor.Info -= OnExecutorOnInfo;
            executor.Verbose -= OnExecutorOnVerbose;
            executor.ErrorReady -= OnExecutorOnErrorReady;
        }

        private void OnExecutorOnDebug(object sender, DebugRecord objects) {
            TestContext.WriteLine(objects.ToString());
        }

        private void OnExecutorOnErrorReady(object sender, ICollection<object> objects) {
            TestContext.WriteLine(objects.ToString());
        }

        private void OnExecutorOnVerbose(object sender, VerboseRecord objects) {
            TestContext.WriteLine(objects.ToString());
        }

        private void OnExecutorOnInfo(object sender, InformationRecord objects) {
            TestContext.WriteLine(objects.ToString());
        }

        private void OnExecutorOnDataReady(object sender, ICollection<PSObject> objects) {
            TestContext.WriteLine(string.Join(";", objects.Select(s => s.ToString())));
        }
    }
}