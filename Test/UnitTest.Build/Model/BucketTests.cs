using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Model {
    [TestClass]
    public class BucketTests {

        [TestMethod]
        public void Directory_segment() {
            var id = new BucketId("3f5cff5e6050f8a0119fd4b66690e5a051ae8deb", "MyBucket", BucketVersion.CurrentTree);

            Assert.AreEqual(@"3f\5cff5e6050f8a0119fd4b66690e5a051ae8deb", id.DirectorySegment);
        }
    }
}
