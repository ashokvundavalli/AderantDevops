using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Model {
    [TestClass]
    public class SourceTreeMetadataTests {

        [TestMethod]
        public void GetBucket_returns_bucket_with_expected_kind() {
            var id1 = new BucketId("1", "A", BucketVersion.CurrentTree);
            var id2 = new BucketId("1", "A", BucketVersion.PreviousTree);

            var metdata = new SourceTreeMetadata();
            metdata.BucketIds = new BucketId[] { id1, id2 };

            var bucket = metdata.GetBucketForCurrentTree("A");

            Assert.AreEqual(BucketVersion.CurrentTree, bucket.Version);
        }
    }
}
