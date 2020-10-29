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
        public void Analysis_result_is_changed_when_when_a_file_is_removed() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            InputFilesDependencyAnalysisResult result = new InputFilesDependencyAnalysisResult();
                
            controller.CorrelateInputs(result,
                new[] { new TrackedInputFile("abc") },
                new[] { new TrackedInputFile("abc"), new TrackedInputFile("def") });

            Assert.IsNotNull(result.IsUpToDate);
            Assert.IsFalse(result.IsUpToDate.Value);
        }

        [TestMethod]
        public void Analysis_result_is_changed_when_when_a_file_is_added() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            InputFilesDependencyAnalysisResult result = new InputFilesDependencyAnalysisResult();

            controller.CorrelateInputs(result,
                new[] { new TrackedInputFile("abc"), new TrackedInputFile("def") },
                new[] { new TrackedInputFile("abc") });

            Assert.IsNotNull(result.IsUpToDate);
            Assert.IsFalse(result.IsUpToDate.Value);
        }


        [TestMethod]
        public void Files_are_examined_when_input_set_is_unchanged() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            InputFilesDependencyAnalysisResult result = new InputFilesDependencyAnalysisResult();

            controller.CorrelateInputs(result,
                new[] { new TrackedInputFile("abc") },
                new[] { new TrackedInputFile("abc") });

            Assert.IsTrue(result.IsUpToDate.Value);
        }

        [TestMethod]
        public void When_no_state_file_then_result_is_up_to_date() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            var areInputsUpToDate = controller.PerformDependencyAnalysis(null, null, null);

            Assert.IsNotNull(areInputsUpToDate);
            Assert.IsTrue(areInputsUpToDate.IsUpToDate.Value);
        }

        [TestMethod]
        public void PaketHashCorrelationMatch() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            const string artifactHash = "BBED1D49615C071DAAAD48AD1FC057E1112148D0";

            List<TrackedInputFile> artifactFiles = new List<TrackedInputFile>(1) { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = artifactHash } };

            var result = new InputFilesDependencyAnalysisResult(false, null);
            controller.CorrelateInputs(result, new [] { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = "BBED1D49615C071DAAAD48AD1FC057E1112148D0" } }, artifactFiles);

            Assert.IsTrue(result.IsUpToDate.Value);
        }

        [TestMethod]
        public void PaketHashCorrelationMismatch() {
            var controller = new TrackedInputFilesController {
                TreatInputAsFiles = false
            };

            const string artifactHash = "A6E011421E1080AD1C4E5F0B7048D64F23B2A67C";

            List<TrackedInputFile> artifactFiles = new List<TrackedInputFile>(1) { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = artifactHash } };

            var result = new InputFilesDependencyAnalysisResult(false, null);
            result.TrackedFiles = artifactFiles;

            controller.CorrelateInputs(result, new[] { new TrackedMetadataFile(Constants.PaketLock) { Sha1 = "BBED1D49615C071DAAAD48AD1FC057E1112148D0" } }, artifactFiles);

            Assert.IsFalse(result.IsUpToDate.Value);
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