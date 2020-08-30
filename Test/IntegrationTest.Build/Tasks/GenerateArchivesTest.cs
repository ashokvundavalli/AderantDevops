using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Aderant.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem(@"TestDeployment\", "TestDeployment")]
    public class GenerateArchivesTest {
        public TestContext TestContext { get; set; }
        private static string sourceFiles;
        private ConcurrentBag<string> destinationFiles;

        [TestInitialize]
        public void TestInitialize() {
            sourceFiles = Path.Combine(TestContext.DeploymentDirectory, @"TestDeployment\");
            destinationFiles = new ConcurrentBag<string> { Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName()) };
        }

        [TestCleanup]
        public void TestCleanup() {
            foreach (string file in destinationFiles) {
                if (Directory.Exists(file)) {
                    Directory.Delete(file, true);
                }
            }
        }

        [TestMethod]
        public void ConstructPathSpecTest() {
            string outputFile = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
            destinationFiles.Add(outputFile);

            List<PathSpec> pathSpecs= GenerateArchives.ConstructPathSpecs(new [] { new TaskItem(sourceFiles) }, new [] { new TaskItem(outputFile) });

            Assert.AreEqual(1, pathSpecs.Count);
            Assert.AreEqual(sourceFiles, pathSpecs[0].Location);
            Assert.AreEqual(outputFile, pathSpecs[0].Destination);
        }

        [TestMethod]
        public void ArchiveNoCompression() {
            string outputFile = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
            destinationFiles.Add(outputFile);

            GenerateArchives task = new GenerateArchives {
                DirectoriesToArchive = new[] { new TaskItem(sourceFiles) },
                OutputArchives = new [] { new TaskItem(outputFile) },
                CompressionLevel = "NoCompression"
            };

            IList<PathSpec> pathSpecs = GenerateArchives.ConstructPathSpecs(task.DirectoriesToArchive, task.OutputArchives);

            long time = TimeArchiveProcess(pathSpecs, CompressionLevel.NoCompression);
            TestContext.WriteLine($"Archived zip files in: {time} milliseconds");

            Assert.IsTrue(File.Exists(pathSpecs[0].Destination));

            time = TimeDecompressionProcess(pathSpecs[0], Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName()));
            TestContext.WriteLine($"Extracted zip files in: {time} milliseconds");
        }

        [TestMethod]
        public void ArchiveFastestCompression() {
            string outputFile = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
            destinationFiles.Add(outputFile);

            GenerateArchives task = new GenerateArchives {
                DirectoriesToArchive = new[] { new TaskItem(sourceFiles) },
                OutputArchives = new[] { new TaskItem(outputFile) },
                CompressionLevel = "Fastest"
            };

            IList<PathSpec> pathSpecs = GenerateArchives.ConstructPathSpecs(task.DirectoriesToArchive, task.OutputArchives);

            long time = TimeArchiveProcess(pathSpecs, CompressionLevel.Fastest);
            TestContext.WriteLine($"Archived zip files in: {time} milliseconds");

            Assert.IsTrue(File.Exists(pathSpecs[0].Destination));

            time = TimeDecompressionProcess(pathSpecs[0], Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName()));
            TestContext.WriteLine($"Extracted zip files in: {time} milliseconds");
        }

        [TestMethod]
        public void ArchiveOptimalCompression() {
            string outputFile = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
            destinationFiles.Add(outputFile);

            GenerateArchives task = new GenerateArchives {
                DirectoriesToArchive = new[] { new TaskItem(sourceFiles) },
                OutputArchives = new[] { new TaskItem(outputFile) },
                CompressionLevel = "Optimal"
            };

            IList<PathSpec> pathSpecs = GenerateArchives.ConstructPathSpecs(task.DirectoriesToArchive, task.OutputArchives);

            long time = TimeArchiveProcess(pathSpecs, CompressionLevel.Optimal);
            TestContext.WriteLine($"Archived zip files in: {time} milliseconds");

            Assert.IsTrue(File.Exists(pathSpecs[0].Destination));

            time = TimeDecompressionProcess(pathSpecs[0], Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName()));
            TestContext.WriteLine($"Extracted zip files in: {time} milliseconds");
        }

        private long TimeArchiveProcess(IList<PathSpec> pathSpecs, CompressionLevel compressionLevel) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            GenerateArchives.ProcessDirectories(pathSpecs, compressionLevel);
            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        private long TimeDecompressionProcess(PathSpec pathSpec, string outputDirectory) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            ZipFile.ExtractToDirectory(pathSpec.Destination, outputDirectory);
            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        [TestMethod]
        public void GenerateUpdateZip() {
            string outputFile = Path.Combine(TestContext.DeploymentDirectory, "update.zip");
            destinationFiles.Add(outputFile);

            GenerateArchives task = new GenerateArchives {
                DirectoriesToArchive = new ITaskItem[] { new TaskItem(sourceFiles) },
                OutputArchives = new ITaskItem[] { new TaskItem(outputFile) },
                CompressionLevel = "Fastest",
                BuildEngine = new MockBuildEngine(),
                CreateManifest = true
            };

            var context = new BuildOperationContext { BuildMetadata = new BuildMetadata { ScmBranch = "branch/Test" } };

            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(DateTime.Now.Ticks.ToString());
                BuildPipelineServiceClient.GetProxy(BuildPipelineServiceHost.PipeId).Publish(context);
                Thread.Sleep(150);
                task.Execute();
            }

            string manifestPath = Path.Combine(sourceFiles, "Manifest.xml");

            Assert.IsTrue(File.Exists(manifestPath));
            Assert.IsTrue(File.Exists(outputFile));
        }

        [TestMethod]
        [DeploymentItem(@"Resources", "Resources")]
        public void GenerateManifestHandlesRelativePaths() {
            var directory = Path.Combine(TestContext.DeploymentDirectory, "Resources");
            var task = new GenerateArchives();

            var context = new BuildOperationContext { BuildMetadata = new BuildMetadata { ScmBranch = "branch/Test" } };
            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(DateTime.Now.Ticks.ToString());
                BuildPipelineServiceClient.GetProxy(BuildPipelineServiceHost.PipeId).Publish(context);

                task.GenerateManifest(directory);
            }

            var manifest = XDocument.Load(Path.Combine(directory, "Manifest.xml"));
            Assert.IsTrue(manifest.Descendants().Any(d => d.Name == "relativePath" && d.Value == "Customization\\NestedFolder"));            
        }
    }
    
    public class MockBuildEngine : IBuildEngine {

        public List<BuildErrorEventArgs> LogErrorEvents = new List<BuildErrorEventArgs>();

        public List<BuildMessageEventArgs> LogMessageEvents = new List<BuildMessageEventArgs>();

        public List<CustomBuildEventArgs> LogCustomEvents = new List<CustomBuildEventArgs>();

        public List<BuildWarningEventArgs> LogWarningEvents = new List<BuildWarningEventArgs>();

        public bool BuildProjectFile(
            string projectFileName, string[] targetNames,
            System.Collections.IDictionary globalProperties,
            System.Collections.IDictionary targetOutputs) {
            return true;
        }

        public int ColumnNumberOfTaskNode => 0;

        public bool ContinueOnError => true;

        public int LineNumberOfTaskNode => 0;

        public void LogCustomEvent(CustomBuildEventArgs e) {
            LogCustomEvents.Add(e);
        }

        public void LogErrorEvent(BuildErrorEventArgs e) {
            LogErrorEvents.Add(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e) {
            LogMessageEvents.Add(e);
        }

        public void LogWarningEvent(BuildWarningEventArgs e) {
            LogWarningEvents.Add(e);
        }

        public string ProjectFileOfTaskNode => "fake ProjectFileOfTaskNode";
    }
}
