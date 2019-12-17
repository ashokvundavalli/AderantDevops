using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class GitVersionTests : BuildTaskTestBase {
        [TestInitialize]
        public void NativeLibraryAvailable() {
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