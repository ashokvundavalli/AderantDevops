using System.IO;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    public class PhysicalFileSystemTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void IsSymlink_returns_true_for_symlink() {
            var fs = new PhysicalFileSystem();

            string linkTarget = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
            var linkPath = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());

            using (Stream stream = fs.CreateFile(linkTarget)) {

            }

            fs.CreateFileSymlink(linkPath, linkTarget, false);
            fs.IsSymlink(linkPath);
        }
    }
}
