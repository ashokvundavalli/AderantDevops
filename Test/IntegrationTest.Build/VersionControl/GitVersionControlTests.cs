﻿using System.Linq;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    public class GitVersionControlTests : GitVersionControlTestBase {

        public override TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            Initialize(context, Resources.CreateRepo, true);
        }

        [TestMethod]
        public void ListChangedFiles() {
            var vc = new GitVersionControl();
            var pendingChanges = vc.GetPendingChanges(null, RepositoryPath);

            Assert.AreEqual("master.txt", pendingChanges.First().Path);
            Assert.AreEqual(FileStatus.Modified, pendingChanges.First().Status);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_without_exception() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(RepositoryPath, "master", "saturn");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Changes);
            Assert.IsNotNull(result.BucketIds);

            Assert.AreEqual(1, result.Changes.Count);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_most_likely_ancestor_when_asked_to_guess() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(RepositoryPath, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);
        }
    }

}
