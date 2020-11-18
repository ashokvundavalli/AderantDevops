using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using UnitTest.Build.Helpers;

namespace UnitTest.Build.StateTracking {

    [TestClass]
    public class TrackedInputFilesControllerTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Target_returns_file_list() {
            Mock<IFileSystem> mock = new Mock<IFileSystem>();
            mock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(true);
            mock.Setup(s => s.OpenFile(It.IsAny<string>())).Returns("".ToStream);

            var controller = new TestTrackedInputFilesController(mock.Object, new TextContextLogger(TestContext)) {
                ProjectReader = XmlReader.Create(new StringReader(Resources.ExampleTrackedInputFiles))
            };

            IReadOnlyCollection<TrackedInputFile> trackedInputFiles;
            trackedInputFiles = controller.GetFilesToTrack(Path.Combine(TestContext.DeploymentDirectory + "\\dummy.txt"), TestContext.DeploymentDirectory);

            Assert.IsNotNull(trackedInputFiles);
            Assert.AreNotEqual(0, trackedInputFiles.Count);
        }

        [TestMethod]
        public void CorrelateInputs_returns_false_when_a_file_is_removed() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;
               
            bool upToDate = controller.CorrelateInputs(new[] { new TrackedInputFile("abc") },
                new[] { new TrackedInputFile("abc"), new TrackedInputFile("def") }, false);

            Assert.IsFalse(upToDate);
        }

        [TestMethod]
        public void CorrelateInputs_returns_false_when_when_a_file_is_added() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            bool upToDate = controller.CorrelateInputs(new[] { new TrackedInputFile("abc"), new TrackedInputFile("def") },
                new[] { new TrackedInputFile("abc") }, false);

            Assert.IsFalse(upToDate);
        }


        [TestMethod]
        public void CorrelateInputs_returns_true_when_input_set_is_unchanged() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            bool upToDate = controller.CorrelateInputs(new[] { new TrackedInputFile("abc") },
                new[] { new TrackedInputFile("abc") }, false);

            Assert.IsTrue(upToDate);
        }

        [TestMethod]
        public void When_no_state_file_then_result_is_up_to_date() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            var areInputsUpToDate = controller.PerformDependencyAnalysis(null, null, null);

            Assert.IsNotNull(areInputsUpToDate);
            Assert.IsTrue(Convert.ToBoolean(areInputsUpToDate.IsUpToDate));
        }

        [TestMethod]
        public void PaketHashCorrelationMatch() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            const string artifactHash = "BBED1D49615C071DAAAD48AD1FC057E1112148D0";

            List<TrackedInputFile> artifactFiles = new List<TrackedInputFile>(1) { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = artifactHash } };

            bool isUpToDate = controller.CorrelateInputs( new [] { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = artifactHash } }, artifactFiles, false);

            Assert.IsTrue(isUpToDate);
        }

        [TestMethod]
        public void PaketHashCorrelationMismatch() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            const string artifactHash = "A6E011421E1080AD1C4E5F0B7048D64F23B2A67C";

            List<TrackedInputFile> artifactFiles = new List<TrackedInputFile>(1) { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = artifactHash } };

            bool isUpToDate = controller.CorrelateInputs(new[] { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = "BBED1D49615C071DAAAD48AD1FC057E1112148D0" } }, artifactFiles, false);

            Assert.IsFalse(isUpToDate);
        }

        [TestMethod]
        public void PerformDependencyAnalysis_IsUpToDate() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            const string sha1 = "A6E011421E1080AD1C4E5F0B7048D64F23B2A67C";

            BuildStateFile[] buildStateFiles = new BuildStateFile[1] {
                new BuildStateFile {
                    TrackedFiles = new List<TrackedInputFile>(1) {
                        new TrackedInputFile("Test") {
                            Sha1 = sha1
                        }
                    }
                }
            };

            var filesToTrack = new List<TrackedInputFile>(1) {
                new TrackedInputFile("Test") {
                    Sha1 = sha1
                }
            };

            InputFilesDependencyAnalysisResult result = controller.PerformDependencyAnalysis(buildStateFiles, filesToTrack, null);

            Assert.IsTrue(Convert.ToBoolean(result.IsUpToDate));
        }

        [TestMethod]
        public void PerformDependencyAnalysis_IsNotUpToDate() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = true
            };

            BuildStateFile[] buildStateFiles = new BuildStateFile[1] {
                new BuildStateFile {
                    TrackedFiles = new List<TrackedInputFile>(1) {
                        new TrackedInputFile("Test") {
                            Sha1 = "A6E011421E1080AD1C4E5F0B7048D64F23B2A67C"
                        }
                    }
                }
            };

            var filesToTrack = new List<TrackedInputFile>(1) {
                new TrackedInputFile("Test") {
                    Sha1 = "60BF068293525D7358E90EEB1C9E80D70CA93B36"
                }
            };

            InputFilesDependencyAnalysisResult result = controller.PerformDependencyAnalysis(buildStateFiles, filesToTrack, null);

            Assert.IsFalse(Convert.ToBoolean(result.IsUpToDate));
        }

        [TestMethod]
        public void PerformDependencyAnalysis_IsUpToDate_Metadata() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            const string artifactHash = "A6E011421E1080AD1C4E5F0B7048D64F23B2A67C";

            BuildStateFile[] buildStateFiles = new BuildStateFile[1] {
                new BuildStateFile {
                    TrackedFiles = new List<TrackedInputFile>(2) {
                        new TrackedInputFile("Test") {
                            Sha1 = artifactHash
                        }
                    },
                    PackageHash = artifactHash,
                    TrackPackageHash = true
                }
            };

            var filesToTrack = new List<TrackedInputFile>(1) {
                new TrackedInputFile("Test") {
                    Sha1 = artifactHash
                }
            };

            var trackedMetadataFiles = new List<TrackedMetadataFile> {
                new TrackedMetadataFile(Constants.PaketLock) {
                    PackageHash = artifactHash,
                    TrackPackageHash = true,
                    Sha1 = artifactHash
                }
            };

            InputFilesDependencyAnalysisResult result = controller.PerformDependencyAnalysis(buildStateFiles, filesToTrack, trackedMetadataFiles);

            Assert.IsTrue(Convert.ToBoolean(result.IsUpToDate));
        }

        [TestMethod]
        public void PerformDependencyAnalysis_IsUpToDate_Metadata_Mismatch() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            BuildStateFile[] buildStateFiles = new BuildStateFile[1] {
                new BuildStateFile {
                    PackageHash = "60BF068293525D7358E90EEB1C9E80D70CA93B36",
                    TrackPackageHash = true
                }
            };

            const string packageHash = "A6E011421E1080AD1C4E5F0B7048D64F23B2A67C";

            var trackedMetadataFiles = new List<TrackedMetadataFile> {
                new TrackedMetadataFile(Constants.PaketLock) {
                    PackageHash = packageHash,
                    TrackPackageHash = true,
                    Sha1 = packageHash
                }
            };

            InputFilesDependencyAnalysisResult result = controller.PerformDependencyAnalysis(buildStateFiles, null, trackedMetadataFiles);

            Assert.IsFalse(Convert.ToBoolean(result.IsUpToDate));
        }
    }

    internal class TestTrackedInputFilesController : TrackedInputFilesController {
        public TestTrackedInputFilesController(IFileSystem fileSystem, ILogger logger) : base(fileSystem, logger) {
        }

        public List<TrackedInputFile> Files { get; set; }

        public override List<TrackedInputFile> GetFilesToTrack(string directory) {
            if (Files != null) {
                return Files;
            }
            return base.GetFilesToTrack(directory);
        }

        public XmlReader ProjectReader { get; set; }

        protected override Project LoadProject(string directoryPropertiesFile, ProjectCollection collection) {
            // LoadProject from reader breaks MSBuildThisFile*
            // https://github.com/Microsoft/msbuild/issues/3030
            return collection.LoadProject(ProjectReader);
        }
    }
}