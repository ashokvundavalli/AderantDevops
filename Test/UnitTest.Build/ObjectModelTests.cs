using System;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ObjectModelTests {

        [TestMethod]
        public void ProjectOutputSnapshotWithFullPath_copy_constructor() {
            var snapshot = new ProjectOutputSnapshot {
                OutputPath = "Foo",
                IsTestProject = true,
                ProjectFile = "Bar",
            };

            var newSnapshot = new ProjectOutputSnapshotWithFullPath(snapshot);

            Assert.AreEqual("Foo", newSnapshot.OutputPath);
            Assert.AreEqual("Foo", newSnapshot.OutputPath);
            Assert.AreEqual("Bar", newSnapshot.ProjectFile);
            Assert.IsTrue(newSnapshot.IsTestProject);
        }
    }
}
