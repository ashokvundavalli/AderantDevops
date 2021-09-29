using System.Linq;
using Aderant.Build.VersionControl;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    public class GitVersionControlNestedChangesTests : GitVersionControlTestBase {

        public GitVersionControlNestedChangesTests() {
            Script = Resources.CreateSubfoldersRepo;
        }

        [Description("Simulates changing one file in a nested directory returning the SHA from the previous commit for the changed directory.")]
        [TestMethod]
        public void GetMetadata_returns_previous_commit_for_folders_with_changes() {
            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, "saturn", "master");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Changes);
            Assert.IsNotNull(result.BucketIds);

            Assert.AreEqual(1, result.Changes.Count);

            //Check the cone dirty bucket uses the previous commit
            var coneBucketId = result.BucketIds.FirstOrDefault(b => b.Tag == "Suite");
            Assert.IsNotNull(coneBucketId);

            using (var repository = new Repository(Repository.Discover(RepositoryPath))) {
                //Get the SHA value of the last commit on master.
                var expectedSha = repository.Branches["master"].Tip.Tree.First().Target.Sha;
                Assert.AreEqual(expectedSha, coneBucketId.Id, "The SHA of the folder with changes should be the previous commit.");
            }
        }
    }
}