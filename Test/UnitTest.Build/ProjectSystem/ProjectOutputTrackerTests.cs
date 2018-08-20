using System;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {

    [TestClass]
    public class ProjectOutputTrackerTests {

        [TestMethod]
        public void Project_key_is_source_relative_path() {
            var collection = new ProjectOutputSnapshot();

            ProjectOutputSnapshotFactory snapshotFactory = new ProjectOutputSnapshotFactory(collection);

            snapshotFactory.SourcesDirectory = @"C:\a\b\c";
            snapshotFactory.ProjectFile = @"C:\a\b\c\d\foo.csproj";

            snapshotFactory.TakeSnapshot(Guid.NewGuid());

            Assert.AreEqual(1, collection.Keys.Count);
            Assert.IsTrue(collection.ContainsKey(@"d\foo.csproj"));
        }

        [TestMethod]
        public void TestProjectType_sets_IsTestProject() {
            var collection = new ProjectOutputSnapshot();
            ProjectOutputSnapshotFactory snapshotFactory = new ProjectOutputSnapshotFactory(collection);
            snapshotFactory.ProjectFile = "foo.csproj";
            snapshotFactory.TestProjectType = "UnitTest";

            var snapshot = snapshotFactory.TakeSnapshot(Guid.NewGuid());
            Assert.IsTrue(snapshot.IsTestProject);
        }

        [TestMethod]
        public void ProjectTypeGuids_sets_IsTestProject() {
            var collection = new ProjectOutputSnapshot();
            ProjectOutputSnapshotFactory snapshotFactory = new ProjectOutputSnapshotFactory(collection);
            snapshotFactory.ProjectFile = "foo.csproj";
            snapshotFactory.ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.TestProject.ToString() };

            var snapshot = snapshotFactory.TakeSnapshot(Guid.NewGuid());
            Assert.IsTrue(snapshot.IsTestProject);
        }
    }
}
