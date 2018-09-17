using Aderant.Build.Packaging;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace IntegrationTest.Build.Packaging {
    [TestClass]
    [DeploymentItem(@"TestDeployment\", "TestDeployment")]
    public class ArtifactServiceTests {
        public TestContext TestContext { get; set; }
        private static string sourceFiles;
        private ConcurrentBag<string> destinationDirectories;

        [TestInitialize]
        public void TestInitialize() {
            sourceFiles = Path.Combine(TestContext.DeploymentDirectory, @"TestDeployment\");
            destinationDirectories = new ConcurrentBag<string> { Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName()) };
        }

        [TestCleanup]
        public void TestCleanup() {
            foreach (string destinationDirectory in destinationDirectories) {
                if (Directory.Exists(destinationDirectory)) {
                    Directory.Delete(destinationDirectory, true);
                }
            }
        }

        #region Implementation
        private IList<PathSpec> InitializePathSpecs(string destinationDirectory) {
            string[] files = Directory.GetFiles(sourceFiles, "*", SearchOption.AllDirectories);

            return files.Select(x => new PathSpec(x, Path.Combine(destinationDirectory, x.Replace(sourceFiles, "")))).ToList();
        }

        private int CopyFilesTaskBlock(ArtifactService artifactService, IList<PathSpec> pathSpecs) {
            ActionBlock<PathSpec> actionBlock = artifactService.CopyFiles(pathSpecs, true);
            actionBlock.Completion.Wait();
            
            return 1;
        }

        private int CopyFilesBudget(ArtifactService artifactService, IList<PathSpec> pathSpecs) {
            foreach (PathSpec pathSpec in pathSpecs) {
                Directory.CreateDirectory(Path.GetDirectoryName(pathSpec.Destination));
                File.Copy(pathSpec.Location, pathSpec.Destination);
            }

            return 1;
        }

        private Stopwatch TimeOperation(Func<ArtifactService, IList<PathSpec>, int> operation, ArtifactService artifactService, IList<PathSpec> pathSpecs) {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            int result = operation(artifactService, pathSpecs);
            stopWatch.Stop();
            TestContext.WriteLine($"File copy completed in: {stopWatch.ElapsedMilliseconds} milliseconds.");

            return stopWatch;
        }
        #endregion

        [TestMethod]
        public void TaskBlockCopyFilesTest() {
            IList<PathSpec> pathSpecs = InitializePathSpecs(destinationDirectories.Last());
            ArtifactService artifactService = new ArtifactService(NullLogger.Default);

            TimeOperation(CopyFilesTaskBlock, artifactService, pathSpecs);
        }

        [TestMethod]
        [Description("TaskBlock copy appears to be more efficient for larger volumes of files.")]
        [Ignore]
        public void TaskBlockCopyPerformance() {
            IList<PathSpec> taskBlockPathSpecs = InitializePathSpecs(destinationDirectories.Last());
            ArtifactService artifactService = new ArtifactService(NullLogger.Default);

            Stopwatch taskBlockCopy = TimeOperation(CopyFilesTaskBlock, artifactService, taskBlockPathSpecs);

            destinationDirectories.Add(Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName()));
            IList<PathSpec> budgetCopyPathSpecs = InitializePathSpecs(destinationDirectories.Last());

            Stopwatch budgetCopy = TimeOperation(CopyFilesBudget, null, budgetCopyPathSpecs);

            Assert.IsTrue(taskBlockCopy.ElapsedMilliseconds < budgetCopy.ElapsedMilliseconds);
        }
    }
}
