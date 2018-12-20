using System.Collections.Generic;
using System.IO;
using System.Xml;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.StateTracking {

    [TestClass]
    public class TrackedInputFilesControllerTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Target_returns_file_list() {
            var controller = new TrackedInputFilesController();

            IReadOnlyCollection<TrackedInputFile> trackedInputFiles;

            using (var reader = XmlReader.Create(new StringReader(Resources.ExampleTrackedInputFiles))) {
                trackedInputFiles = controller.GetFilesToTrack(reader, null, TestContext.DeploymentDirectory);
            }

            Assert.IsNotNull(trackedInputFiles);
            Assert.AreNotEqual(0, trackedInputFiles.Count);
        }

        [TestMethod]
        public void Analysis_result_is_changed_when_when_a_file_is_removed() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            InputFilesDependencyAnalysisResult result = controller.CorrelateInputs(
                new[] { new TrackedInputFile("abc") },
                new[] { new TrackedInputFile("abc"), new TrackedInputFile("def") });

            Assert.IsNotNull(result.UpToDate);
            Assert.IsFalse(result.UpToDate.Value);
        }

        [TestMethod]
        public void Analysis_result_is_changed_when_when_a_file_is_added() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            InputFilesDependencyAnalysisResult result = controller.CorrelateInputs(
                new[] { new TrackedInputFile("abc"), new TrackedInputFile("def") },
                new[] { new TrackedInputFile("abc") });

            Assert.IsNotNull(result.UpToDate);
            Assert.IsFalse(result.UpToDate.Value);
        }


        [TestMethod]
        public void Files_are_examined_when_input_set_is_unchanged() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            InputFilesDependencyAnalysisResult result = controller.CorrelateInputs(
                new[] { new TrackedInputFile("abc") },
                new[] { new TrackedInputFile("abc") });
        }

        [TestMethod]
        public void When_no_state_file_then_result_is_null() {
            var controller = new TrackedInputFilesController();
            controller.TreatInputAsFiles = false;

            var areInputsUpToDate = controller.PerformDependencyAnalysis(null, null);

            Assert.IsNotNull(areInputsUpToDate);
            Assert.IsNull(areInputsUpToDate.UpToDate);
        }
    }
}