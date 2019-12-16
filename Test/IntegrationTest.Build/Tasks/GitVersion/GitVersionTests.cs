using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GitVersion {
    [TestClass]
    public class GitVersionTests : MSBuildIntegrationTestBase {
        [TestInitialize]
        [DeploymentItem("git2-7ce88e6.dll")]
        public void NativeLibraryAvailable() {
            var foo = typeof(Aderant.Build.Tasks.GitVersion);
            string nativeLibraryPath = LibGit2Sharp.GlobalSettings.NativeLibraryPath;

            Assert.IsTrue(Directory.Exists(nativeLibraryPath));
        }

        [TestMethod]
        public void GitVersion_runs_without_exception() {
            RunTarget("GitVersion");

            Assert.IsFalse(Logger.HasRaisedErrors);
        }
    }
}