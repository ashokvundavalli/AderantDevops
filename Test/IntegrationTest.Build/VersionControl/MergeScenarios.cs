using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {
    [TestClass]
    public class MergeScenarios : GitVersionControlTestBase {

        public MergeScenarios() {
            Script = Resources.Merge;
        }

        [TestMethod]
        [Description("Simulates merging a branch and ensures that the change set does not have new files from the merge added to the change set.")]
        public void GetSourceTreeInfo_returns_most_likely_ancestor_after_merge() {
            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, null, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Changes.Count);
            Assert.AreEqual(2, result.BucketIds.Count);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);
        }
    }
}