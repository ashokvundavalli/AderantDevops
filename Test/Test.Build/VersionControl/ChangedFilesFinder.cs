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

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            using (var ps = PowerShell.Create()) {
                ps.AddScript("cd " + context.DeploymentDirectory.Quote());
                ps.AddScript(Resources.CreateRepo);
                ps.Invoke();
            }
        }

        [TestMethod]
        public void ListChangedFiles() {
            var vc = new GitVersionControl();
            var pendingChanges = vc.GetPendingChanges(new BuildMetadata(), Path.Combine(TestContext.DeploymentDirectory, "Repo"));

            Assert.AreEqual("somefile.txt", pendingChanges.First().Path);
            Assert.AreEqual(FileStatus.Modified, pendingChanges.First().Status);
        }
    }

}
