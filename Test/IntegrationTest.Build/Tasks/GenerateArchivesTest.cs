using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Aderant.Build.Packaging;
using Aderant.Build.Tasks;
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
    }
}
