using System.IO;
using Aderant.Build.Tasks.BuildTime.Sequencer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("Aderant.BuildTime.Tasks.dll")]
    [DeploymentItem("IntegrationTest.targets")]
    [DeploymentItem("Aderant.Build.Common.targets")]
    [DeploymentItem("SystemUnderTest")]
    public class GitChangesetTests {
        [TestInitialize]
        public void NativeLibraryAvailable() {
            var foo = typeof(ChangesetResolver);
            string nativeLibraryPath = LibGit2Sharp.GlobalSettings.NativeLibraryPath;

            Assert.IsTrue(Directory.Exists(nativeLibraryPath));
        }

        [TestMethod]
        public void GitChangesetExecuteTest() {
            var changeset = new ChangesetResolver(@"C:\Git\Deployment");
            if (changeset.FriendlyBranchName != "master") {
                Assert.IsNotNull(changeset.ChangedFiles);
            }
        }
    }
}