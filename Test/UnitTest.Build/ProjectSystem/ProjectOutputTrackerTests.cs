using System;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {

    [TestClass]
    public class ProjectOutputTrackerTests {

        [TestMethod]
        public void Project_key_is_source_relative_path() {
            var service = new BuildPipelineServiceImpl();

            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder();

            snapshotBuilder.SourcesDirectory = @"C:\a\b\c";
            snapshotBuilder.ProjectFile = @"C:\a\b\c\d\foo.csproj";

            var outputFilesSnapshot = snapshotBuilder.BuildSnapshot(Guid.NewGuid());

            service.RecordProjectOutputs(outputFilesSnapshot);

            Assert.AreEqual(1, service.Outputs.Keys.Count);
            Assert.IsTrue(service.Outputs.ContainsKey(@"d\foo.csproj"));
        }

        [TestMethod]
        public void TestProjectType_sets_IsTestProject() {
            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder();
            snapshotBuilder.ProjectFile = "foo.csproj";
            snapshotBuilder.TestProjectType = "UnitTest";

            var snapshot = snapshotBuilder.BuildSnapshot(Guid.NewGuid());
            Assert.IsTrue(snapshot.IsTestProject);
        }

        [TestMethod]
        public void ProjectTypeGuids_sets_IsTestProject() {
            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder();
            snapshotBuilder.ProjectFile = "foo.csproj";
            snapshotBuilder.ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.TestProject.ToString() };

            var snapshot = snapshotBuilder.BuildSnapshot(Guid.NewGuid());
            Assert.IsTrue(snapshot.IsTestProject);
        }

        [TestMethod]
        public void RecordOutputs_removes_obj_files() {
            var outputs = ProjectOutputSnapshotBuilder.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { @"obj/baz.dll" }, @"..\..\bin", "obj");

            Assert.AreEqual(0, outputs.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_keeps_output() {

            var outputFilesSnapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { "obj/baz.dll", "baz.dll" }, @"..\..\bin", "obj");

            Assert.AreEqual(1, outputFilesSnapshot.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_is_deterministic() {
            var snapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { "B", "10000", "A", "001", "Z", "1" }, @"..\..\bin", "obj");

            Assert.AreEqual("001", snapshot.FilesWritten[0]);
            Assert.AreEqual("1", snapshot.FilesWritten[1]);
            Assert.AreEqual("10000", snapshot.FilesWritten[2]);
            Assert.AreEqual("A", snapshot.FilesWritten[3]);
            Assert.AreEqual("B", snapshot.FilesWritten[4]);
            Assert.AreEqual("Z", snapshot.FilesWritten[5]);
        }

        [TestMethod]
        public void FileWrites_are_cleaned() {
            var snapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { @"foo\\bin\\baz.dll" }, @"..\..\bin", "obj");

            Assert.AreEqual(@"foo\bin\baz.dll", snapshot.FilesWritten[0]);
        }
    }
}
