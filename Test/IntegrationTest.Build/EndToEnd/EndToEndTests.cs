using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using Aderant.Build;
using Aderant.Build.Ipc;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {

    [TestClass]
    [DeploymentItem("EndToEnd\\Resources", "Resources")]
    public class EndToEndTests : MSBuildIntegrationTestBase {

        [TestMethod]
        public void FullTest() {
            AddFilesToNewGitRepo(DeploymentItemsDirectory);
            

            var context = new BuildOperationContext();
            context.BuildMetadata = new BuildMetadata();
            context.SourceTreeMetadata = GetSourceTreeMetadata();

            var properties = new Dictionary<string, string> {
                { "BuildSystemInTestMode", bool.TrueString },
                { "BuildScriptsDirectory", TestContext.DeploymentDirectory + "\\" },
                { "CompileBuildSystem", bool.FalseString },
                { "ProductManifestPath", Path.Combine(DeploymentItemsDirectory, "Expertmanifest.xml") },
                { "SolutionRoot", Path.Combine(DeploymentItemsDirectory) }
            };

            context.BuildScriptsDirectory = properties["BuildScriptsDirectory"];
            context.BuildSystemDirectory = Path.Combine(TestContext.DeploymentDirectory, @"..\..\");

            using (var process = Process.GetCurrentProcess()) {
                var name = MemoryMappedFileReaderWriter.WriteData("TestContext" + process.Id, context);
                properties[WellKnownProperties.ContextFileName] = name;
            }

            RunTarget("EndToEnd", properties);
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var versionControl = new GitVersionControl();
            return versionControl.GetMetadata(DeploymentItemsDirectory, "", "");
        }

        private void AddFilesToNewGitRepo(string testContextDeploymentDirectory) {
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
