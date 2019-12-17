using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    public class GitVersionControlTests : GitVersionControlTestBase {

        public override TestContext TestContext { get; set; }


        [TestMethod]
        public void GetSourceTreeInfo_returns_without_exception() {
            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, "master", "saturn");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Changes);
            Assert.IsNotNull(result.BucketIds);

            Assert.AreEqual(1, result.Changes.Count);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_most_likely_ancestor_when_asked_to_guess() {
            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);
        }

        [Description("Simulates manually queuing a branch build on server, where fromBranch is not null but toBranch is null.")]
        [TestMethod]
        public void GetSourceTreeInfo_returns_most_likely_ancestor_for_branch_build() {
            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, "saturn", null);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Changes.Count);
            Assert.AreEqual(2, result.BucketIds.Count);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);
        }
    }
}