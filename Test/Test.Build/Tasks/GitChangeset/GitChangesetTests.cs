using System.IO;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GitChangeset {
    [TestClass]
    public class GitChangesetTests : BuildTaskTestBase {
        [TestInitialize]
        public void NativeLibraryAvailable() {
            var foo = typeof(ChangesetResolver);
            string nativeLibraryPath = LibGit2Sharp.GlobalSettings.NativeLibraryPath;

            Assert.IsTrue(Directory.Exists(nativeLibraryPath));
        }

        [TestMethod]
        public void GitChangesetExecuteTest() {
            var testContextDeploymentDirectory = TestContext.TestRunDirectory;

            var changeset = new ChangesetResolver(null, @"C:\Git\Deployment");
            if (changeset.FriendlyBranchName != "master") {
                Assert.IsNotNull(changeset.ChangedFiles);
            }
        }
    }
}