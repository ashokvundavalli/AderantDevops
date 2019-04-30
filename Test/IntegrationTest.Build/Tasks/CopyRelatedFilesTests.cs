using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class CopyRelatedFilesTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Resources", "Resources")]
        public void CopyRelatedFilesWithWildcard() {
            string sourceFile = "*Aderant.Query.*dll";
            var relatedFiles = new List<string> { "ExpertQuery.SqlViews.sql", "*Aderant.Query.*.dll", "*\\Customization*" };

            var resourcesFolder = Path.Combine(TestContext.DeploymentDirectory, "Resources", "BinFiles");
            var task = new CopyRelatedFiles { SourceLocation = resourcesFolder + Path.DirectorySeparatorChar, BuildEngine = new MockBuildEngine() };

            var destination = Path.Combine(TestContext.DeploymentDirectory, "WildCardCopyTest");
            Directory.CreateDirectory(destination);
            var triggerFile = "Aderant.Query.dll";
            File.Create(Path.Combine(destination, triggerFile));

            task.Destination = destination;

            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(DateTime.Now.Ticks.ToString());
                var relatedFilesDictionary = new Dictionary<string, List<string>> { { sourceFile, relatedFiles } };
                BuildPipelineServiceClient.GetProxy(BuildPipelineServiceHost.PipeId).RecordRelatedFiles(relatedFilesDictionary);

                task.ExecuteTask();
            }

            var files = Directory.GetFiles(destination, "*", SearchOption.AllDirectories);
            Assert.AreEqual(5, files.Length);
            Assert.IsTrue(files.Any(f => f.EndsWith("Aderant.Query.Resources.dll")));
            Assert.IsTrue(files.Any(f => f.EndsWith("Services.Query.Custom.zip")));
            Assert.IsTrue(files.Any(f => f.EndsWith("ExpertQuery.SqlViews.sql")));
            Assert.IsTrue(files.Any(f => f.EndsWith("Aderant.Query.dll")));
            Assert.IsTrue(files.Any(f => f.EndsWith("TestFile.txt")));
        }

        [TestMethod]
        [DeploymentItem(@"Resources", "Resources")]
        public void CopyRelatedFilesWithNoWildcard() {
            string sourceFile = "Aderant.Query.dll";
            var relatedFiles = new List<string> { "ExpertQuery.SqlViews.sql", "Aderant.Query.Resources.dll" };

            var resourcesFolder = Path.Combine(TestContext.DeploymentDirectory, "Resources", "BinFiles");
            var task = new CopyRelatedFiles { SourceLocation = resourcesFolder + Path.DirectorySeparatorChar, BuildEngine = new MockBuildEngine() };

            var destination = Path.Combine(TestContext.DeploymentDirectory, "ConcreteCopyTest");
            Directory.CreateDirectory(destination);
            var triggerFile = "Aderant.Query.dll";
            File.Create(Path.Combine(destination, triggerFile));

            task.Destination = destination;

            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(DateTime.Now.Ticks.ToString());
                var relatedFilesDictionary = new Dictionary<string, List<string>> { { sourceFile, relatedFiles } };
                BuildPipelineServiceClient.GetProxy(BuildPipelineServiceHost.PipeId).RecordRelatedFiles(relatedFilesDictionary);

                task.ExecuteTask();
            }

            var files = Directory.GetFiles(destination, "*", SearchOption.AllDirectories);
            Assert.AreEqual(3, files.Length);
            Assert.IsTrue(files.Any(f => f.EndsWith("Aderant.Query.Resources.dll")));
            Assert.IsTrue(files.Any(f => f.EndsWith("ExpertQuery.SqlViews.sql")));
            Assert.IsTrue(files.Any(f => f.EndsWith("Aderant.Query.dll")));
        }
    }
}
