using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {

    [TestClass]
    [DeploymentItem(@"TestDeployment\x86\", "x86")]
    [DeploymentItem(@"TestDeployment\x64\", "x64")]
    public class GitVersionTestBase {
        private static Collection<PSObject> results;

        public TestContext TestContext { get; set; }

        protected static string Repo { get; set; }

        protected static void Initialize(TestContext context, string resources) {
            var fileTimeUtc = DateTime.UtcNow.ToFileTimeUtc();

            var testDirectory = Path.Combine(context.DeploymentDirectory, fileTimeUtc.ToString());

            Directory.CreateDirectory(testDirectory);

            using (var ps = PowerShell.Create()) {
                ps.AddScript($"cd {testDirectory.Quote()}");
                ps.AddScript(resources);
                results = ps.Invoke();

            }

            Repo = $"{testDirectory}\\Repo";
        }

        [TestInitialize]
        public void DumpGitStatus() {
            foreach (var psObject in results) {
                TestContext.WriteLine(psObject.ToString());
            }
        }
    }

    [TestClass]
    public class GitVersionControlTests : GitVersionTestBase {

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            Initialize(context, Resources.CreateRepo);
        }

        [TestMethod]
        public void ListChangedFiles() {
            var vc = new GitVersionControl();
            var pendingChanges = vc.GetPendingChanges(null, Repo);

            Assert.AreEqual("master.txt", pendingChanges.First().Path);
            Assert.AreEqual(FileStatus.Modified, pendingChanges.First().Status);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_without_exception() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(Repo, "master", "saturn");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Changes);
            Assert.IsNotNull(result.BucketIds);

            Assert.AreEqual(1, result.Changes.Count);
        }

        [TestMethod]
        public void GetSourceTreeInfo_returns_most_likely_ancestor_when_asked_to_guess() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(Repo, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);
        }
    }

    /// <summary>
    /// These tests are intended to cover the discovery of reusable build trees
    /// </summary>
    [TestClass]
    public class TreeReuseTests : GitVersionTestBase {

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            Initialize(context, Resources.CommitGraphWalking);
        }

        [TestMethod]
        public void Tree_sha_is_stable() {
            var vc = new GitVersionControl();
            var result = vc.GetMetadata(Repo, "", "");

            Assert.IsNotNull(result);
            Assert.AreEqual("refs/heads/master", result.CommonAncestor);

            Assert.AreEqual("885048a4c6ce8fc35723b5fbe4ea99ab5948122b", result.GetBucket(BucketId.Current).Id);
            Assert.AreEqual("1e6931e5a4e7e03f8afe2035ac19e90f56a425f5", result.GetBucket(BucketId.Previous).Id);
            Assert.AreEqual("c5d3a09a01a42ee7f4b04ab421e529fe02bc9b0f", result.GetBucket(BucketId.ParentsParent).Id);
        }
    }
}
