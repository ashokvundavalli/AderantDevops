using System.IO;
using Aderant.Build.Tasks;
using Aderant.BuildTime.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("Aderant.BuildTime.Tasks.dll")]
    public class GitChangesetTests : BuildTaskTestBase {
        [TestInitialize]
        public void NativeLibraryAvailable() {
            var foo = typeof(GitChangeset);
            string nativeLibraryPath = LibGit2Sharp.GlobalSettings.NativeLibraryPath;

            Assert.IsTrue(Directory.Exists(nativeLibraryPath));
        }

        [TestMethod]
        public void GitChangeset_runs_without_exception() {
            RunTarget("GitChangeset");

            Assert.IsFalse(Logger.HasRaisedErrors);
        }

        [TestMethod]
        public void GitChangesetExecuteTest() {
            var changeset = new GitChangeset() {WorkingDirectory = @"C:\Git\Deployment" };
            changeset.Execute();
        }
    }
}