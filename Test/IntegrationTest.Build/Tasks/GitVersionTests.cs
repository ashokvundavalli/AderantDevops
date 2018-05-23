using System.IO;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("Aderant.BuildTime.Tasks.dll")]
    public class GitVersionTests : BuildTaskTestBase {
        [TestInitialize]
        public void NativeLibraryAvailable() {
            var foo = typeof(GitVersion);
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