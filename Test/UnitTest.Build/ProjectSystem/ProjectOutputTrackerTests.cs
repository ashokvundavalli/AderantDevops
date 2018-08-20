using System;
using Aderant.Build;
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
    }
}
