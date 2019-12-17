using Aderant.Build;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class ArtifactDropPathBuilderTests {

        [TestMethod]
        public void Path_contains_branch_name() {
            ArtifactDropPathBuilder artifactDropPathBuilder = new ArtifactDropPathBuilder();
            artifactDropPathBuilder.PrimaryDropLocation = @"\\foo\bar";
            artifactDropPathBuilder.PullRequestDropLocation = @"\\foo\pulls\";

            var path = artifactDropPathBuilder.CreatePath("MyArtifact", new BuildMetadata { BuildId = 1, ScmBranch = "refs/heads/master" });

            Assert.AreEqual(@"\\foo\bar\refs\heads\master\1\MyArtifact", path);
        }
    }
}
