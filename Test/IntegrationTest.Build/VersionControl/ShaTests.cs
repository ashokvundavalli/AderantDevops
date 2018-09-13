using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    public class ShaTests : GitVersionControlTestBase {

        public override TestContext TestContext { get; set; }

        [TestInitialize]
        public void ClassInitialize() {
            Initialize(TestContext, Resources.CommitGraphWalking, false);
        }

        [TestMethod]
        public void Tree_sha_is_stable() {
            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);

            Assert.AreEqual("885048a4c6ce8fc35723b5fbe4ea99ab5948122b", result.GetBucket(BucketId.Current).Id);
            Assert.AreEqual("1e6931e5a4e7e03f8afe2035ac19e90f56a425f5", result.GetBucket(BucketId.Previous).Id);
        }

        [TestMethod]
        public void Per_directory_bucket_sha_is_stable() {
            var createScript = @"
New-Item -ItemType Directory ""Dir1""
Add-Content -Path ""Dir1\dir1.txt"" -Value  ""123""

New-Item -ItemType Directory ""Dir2""
Add-Content -Path ""Dir2\dir2.txt"" -Value  ""456""
& git add .
& git commit -m ""Add folders""";

            RunPowerShell(TestContext, createScript);

            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(RepositoryPath, "", "");

            Assert.AreEqual("12ea309af9a27ee662c636f4b82246f8619b3bee", result.GetBucket("Dir1").Id);
            Assert.AreEqual("53d9f188d4c8cb79c6b98bc56e5c629def625ca1", result.GetBucket("Dir2").Id);

            var updateScript = @"
Add-Content -Path ""Dir2\dir2.txt"" -Value  ""456123""
& git add .
& git commit -m ""Add folders""";

            RunPowerShell(TestContext, updateScript);

            result = vc.GetMetadata(RepositoryPath, "", "");
            Assert.AreEqual("12ea309af9a27ee662c636f4b82246f8619b3bee", result.GetBucket("Dir1").Id);
            Assert.AreEqual("ccf18c9c16647bcc54c09191ac44e566a4a760d8", result.GetBucket("Dir2").Id);
        }
    }
}
