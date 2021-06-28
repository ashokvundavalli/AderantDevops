using Aderant.Build.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class PathHelperTest {

        [TestMethod]
        public void GetBranch_canonicalizes_dev_branch_name() {
            string branch = PathHelper.GetBranch(@"dev\framework");

            Assert.AreEqual(@"Dev\Framework", branch);
        }

        [TestMethod]
        public void GetBranch_canonicalizes_branch_name() {
            string branch = PathHelper.GetBranch(@"main");

            Assert.AreEqual(@"Main", branch);
        }

        [TestMethod]
        public void GetBranch_canonicalizes_branch_name_unc() {
            string branch = PathHelper.GetBranch(@"\\aderant.com\ExpertSuite\Dev\Framework");

            Assert.AreEqual(@"Dev\Framework", branch);
        }

        [TestMethod]
        public void Automation_branch_full_unc() {
            string branch = PathHelper.GetBranch(@"\\aderant.com\packages\Infrastructure\Automation\UIAutomation\UIAutomation.Framework\5.3.1.0\5.3.5568.49992\Bin\Module");

            Assert.AreEqual(@"Automation\UIAutomation", branch, true);
        }
    }
}
