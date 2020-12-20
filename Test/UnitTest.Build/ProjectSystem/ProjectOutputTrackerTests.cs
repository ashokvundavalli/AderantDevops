using System.IO;
using Aderant.Build;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {

    [TestClass]
    public class ProjectOutputTrackerTests {

        [TestMethod]
        public void Project_key_is_source_relative_path() {
            var service = new BuildPipelineServiceImpl();

            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder(@"C:\a\b\c", @"C:\a\b\c\d\foo.csproj",
                null, @"C:\a\b\c\bin\module\", null, null, null, null);


            var outputFilesSnapshot = snapshotBuilder.BuildSnapshot();

            service.RecordProjectOutputs(outputFilesSnapshot);

            Assert.AreEqual(1, service.Outputs.Keys.Count);
            Assert.IsTrue(service.Outputs.ContainsKey(@"d\foo.csproj"));
        }

        [TestMethod]
        public void Project_output_path_is_relative() {
            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder(@"C:\a\b\c", @"C:\a\b\c\d\foo.csproj",
                null, @"C:\a\b\c\bin\module\", null, null, null, null);

            ProjectOutputSnapshot outputFilesSnapshot = snapshotBuilder.BuildSnapshot();

            Assert.IsFalse(Path.IsPathRooted(outputFilesSnapshot.OutputPath));
        }

        [TestMethod]
        public void TestProjectType_sets_IsTestProject() {
            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder(null, "foo.csproj", null,
                null, null, null, "UnitTest", null);

            var snapshot = snapshotBuilder.BuildSnapshot();
            Assert.IsTrue(snapshot.IsTestProject);
        }

        [TestMethod]
        public void ProjectTypeGuids_sets_IsTestProject() {
            ProjectOutputSnapshotBuilder snapshotBuilder = new ProjectOutputSnapshotBuilder(null, "foo.csproj", null, null,
                null, new[] { WellKnownProjectTypeGuids.TestProject.ToString() }, null, null);

            var snapshot = snapshotBuilder.BuildSnapshot();
            Assert.IsTrue(snapshot.IsTestProject);
        }

        [TestMethod]
        public void RecordOutputs_removes_obj_files() {
            var outputs = ProjectOutputSnapshotBuilder.RecordProjectOutputs("", "Foo", new[] { @"obj/baz.dll" }, @"..\..\bin", "obj".ToStringArray());

            Assert.AreEqual(0, outputs.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_keeps_output() {

            var outputFilesSnapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs("", "Foo", new[] { "obj/baz.dll", "baz.dll" }, @"..\..\bin", "obj".ToStringArray());

            Assert.AreEqual(1, outputFilesSnapshot.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_is_deterministic() {
            var snapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs("", "Foo", new[] { "B", "10000", "A", "001", "Z", "1" }, @"..\..\bin", "obj".ToStringArray());

            Assert.AreEqual("001", snapshot.FilesWritten[0]);
            Assert.AreEqual("1", snapshot.FilesWritten[1]);
            Assert.AreEqual("10000", snapshot.FilesWritten[2]);
            Assert.AreEqual("A", snapshot.FilesWritten[3]);
            Assert.AreEqual("B", snapshot.FilesWritten[4]);
            Assert.AreEqual("Z", snapshot.FilesWritten[5]);
        }

        [TestMethod]
        public void FileWrites_are_cleaned() {
            var snapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs("", "Foo", new[] { @"foo\\bin\\baz.dll" }, @"..\..\bin", "obj".ToStringArray());

            Assert.AreEqual(@"foo\bin\baz.dll", snapshot.FilesWritten[0]);
        }

        [TestMethod]
        public void FileWrites_are_relative_paths() {
            var snapshot = ProjectOutputSnapshotBuilder.RecordProjectOutputs(
                "C:\\B\\516\\2\\s\\Services.Communication",
                "C:\\B\\516\\2\\s\\Services.Communication\\Src\\Aderant.Notification\\Aderant.Notification.cspoj",
                "C:\\B\\516\\2\\s\\Services.Communication\\Bin\\Module\\Aderant.Notification.Api.zip".ToStringArray(),
                "..\\..\\bin",
                "obj".ToStringArray());

            Assert.AreEqual(@"..\..\Bin\Module\Aderant.Notification.Api.zip", snapshot.FilesWritten[0]);
        }
    }
}
