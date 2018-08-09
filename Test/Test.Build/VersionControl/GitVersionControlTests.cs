using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FileStatus = Aderant.Build.VersionControl.FileStatus;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    [DeploymentItem(@"SystemUnderTest\x86\", "x86")]
    [DeploymentItem(@"SystemUnderTest\x64\", "x64")]
    public class GitVersionControlTests {
        private static string repo;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            using (var ps = PowerShell.Create()) {
                ps.AddScript("cd " + context.DeploymentDirectory.Quote());
                ps.AddScript(Resources.CreateRepo);
                ps.Invoke();
            }

            repo = Path.Combine(context.DeploymentDirectory, "Repo");
        }

        [TestMethod]
        public void ListChangedFiles() {
            var vc = new GitVersionControl();
            var pendingChanges = vc.GetPendingChanges(new BuildMetadata(), repo);

            Assert.AreEqual("master.txt", pendingChanges.First().Path);
            Assert.AreEqual(FileStatus.Modified, pendingChanges.First().Status);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_without_exception() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(repo, "master", "saturn");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Changes);
            Assert.IsNotNull(result.BucketIds);

            Assert.AreEqual(2, result.Changes.Count);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_most_likely_ancestor_when_asked_to_guess() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(repo, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);
        }
    }

}
