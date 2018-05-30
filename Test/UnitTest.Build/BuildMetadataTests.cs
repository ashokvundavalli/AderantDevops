using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class BuildMetadataTests {

        [TestMethod]
        public void IsPullRequest() {
            var metadata = new BuildMetadata();

            Assert.IsFalse(metadata.IsPullRequest);
        }
    }

}