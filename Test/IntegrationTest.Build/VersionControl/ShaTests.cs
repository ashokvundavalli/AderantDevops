using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    public class ShaTests : GitVersionControlTestBase {

        public override TestContext TestContext { get; set; }

        [TestMethod]
        public void Tree_sha_is_stable() {
            var repositoryPath = RunPowerShellInIsolatedDirectory(TestContext, Resources.CommitGraphWalking);

            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(repositoryPath, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);

            Assert.AreEqual("885048a4c6ce8fc35723b5fbe4ea99ab5948122b", result.GetBucket(BucketId.Current, BucketKind.CurrentCommit).Id);
            Assert.AreEqual("1e6931e5a4e7e03f8afe2035ac19e90f56a425f5", result.GetBucket(BucketId.Previous, BucketKind.PreviousCommit).Id);
        }

        [TestMethod]
        public void Per_directory_bucket_sha_is_stable() {
            var createScript = @"

[string]$cwd = (Get-Location)

New-Item -ItemType Directory -Path ($cwd + '\Dir1')
$dir1 = [Management.Automation.WildcardPattern]::Unescape($cwd + '.\Dir1\dir1.txt')
Add-Content -LiteralPath $dir1 -Value '123' -Force

New-Item -ItemType Directory -Path ($cwd + '\Dir2')
$dir2 = [Management.Automation.WildcardPattern]::Unescape($cwd + '.\Dir2\dir2.txt')
Add-Content -LiteralPath $dir2 -Value '456' -Force

& git init .
& git add .
& git commit -m 'Add folders'
";

            var repositoryPath = RunPowerShellInIsolatedDirectory(TestContext, createScript);

            var vc = new GitVersionControlService();
            var result = vc.GetMetadata(repositoryPath, "", "");

            const string dir2Sha = "53d9f188d4c8cb79c6b98bc56e5c629def625ca1";
            Assert.AreEqual("12ea309af9a27ee662c636f4b82246f8619b3bee", result.GetBucket("Dir1", BucketKind.CurrentCommit).Id);
            Assert.AreEqual(dir2Sha, result.GetBucket("Dir2", BucketKind.PreviousCommit).Id);

            var updateScript = @"
[string]$cwd = (Get-Location)

$dir2 = [Management.Automation.WildcardPattern]::Unescape($cwd + '.\Dir2\dir2.txt')

Add-Content -LiteralPath $dir2 -Value '456123'
& git add .
& git commit -m 'Change content'";

            RunPowerShellInDirectory(TestContext, updateScript, repositoryPath);

            result = vc.GetMetadata(repositoryPath, "", "");
            Assert.AreEqual("12ea309af9a27ee662c636f4b82246f8619b3bee", result.GetBucket("Dir1", BucketKind.CurrentCommit).Id);
            Assert.AreEqual(dir2Sha, result.GetBucket("Dir2", BucketKind.PreviousCommit).Id); //Dir2 has a change, so the previous SHA should be used for the cache.
        }


    }
}
