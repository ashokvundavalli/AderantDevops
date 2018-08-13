using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Aderant.Build;
using Aderant.Build.Ipc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {

    [TestClass]
    [DeploymentItem("EndToEnd\\Resources", "Resources")]
    public class EndToEndTests : MSBuildIntegrationTestBase {

        [TestMethod]
        public void FullTest() {
            var context = new BuildOperationContext();
            context.BuildMetadata = new BuildMetadata();

            var properties = new Dictionary<string, string> {
                { "BuildSystemInTestMode", bool.TrueString },
                { "BuildScriptsDirectory", TestContext.DeploymentDirectory + "\\" },
                { "CompileBuildSystem", bool.FalseString },
                { "ProductManifestPath", Path.Combine(Root, "Expertmanifest.xml") },
                { "SolutionRoot", Path.Combine(Root) }
            };

            context.BuildScriptsDirectory = properties["BuildScriptsDirectory"];
            context.BuildSystemDirectory = Path.Combine(TestContext.DeploymentDirectory, @"..\..\");

            using (var process = Process.GetCurrentProcess()) {
                var name = MemoryMappedFileReaderWriter.WriteData("TestContext" + process.Id, context);
                properties[WellKnownProperties.ContextFileName] = name;
            }

            RunTarget("EndToEnd", properties);
        }

        public string Root {
            get {
                return Path.Combine(TestContext.DeploymentDirectory, "Resources", "Source");
            }
        }
    }
}
