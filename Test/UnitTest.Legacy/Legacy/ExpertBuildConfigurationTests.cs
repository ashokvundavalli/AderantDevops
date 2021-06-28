using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ExpertBuildConfigurationTests {

        [TestMethod]
        public void ServerPathToModule_is_null_when_no_module_name_set() {
            var configuration = new ExpertBuildConfiguration("Dev/802Grad");

            Assert.IsNull(configuration.SourceControlPathToModule);
        }

        [TestMethod]
        public void ServerPathToModule_is_not_null() {
            var configuration = new ExpertBuildConfiguration("Dev/802Grad");
            configuration.ModuleName = "Applications.Deployment";
            configuration.TeamProject = "ExpertSuite";

            Assert.AreEqual("$/ExpertSuite/Dev/802Grad/Modules/Applications.Deployment", configuration.SourceControlPathToModule);
        }

        [TestMethod]
        public void Branch_name_fixup_is_applied_by_constructor() {
            var configuration = new ExpertBuildConfiguration("Dev\\802Grad"); // Usage of the unexpected branch name separator
            configuration.ModuleName = "Applications.Deployment";
            configuration.TeamProject = "ExpertSuite";

            Assert.AreEqual("$/ExpertSuite/Dev/802Grad/Modules/Applications.Deployment", configuration.SourceControlPathToModule);
        }

        [TestMethod]
        public void Branch_is_removed_drop_drop_if_needed() {
            ExpertBuildConfiguration buildConfiguration = new ExpertBuildConfiguration("Dev\\MyBranch") {
                ModuleName = "Foo",
                DropLocation = @"\\aderant.com\ExpertSuite\Dev\MyBranch"
            };

            ExpertBuildDetail detail = new ExpertBuildDetail("99.99.99.99", "1.0.0.0", buildConfiguration);

            Assert.AreEqual(@"\\aderant.com\ExpertSuite\Dev\MyBranch\Foo\99.99.99.99\1.0.0.0", detail.DropLocation);
        }

        [TestMethod]
        public void Main_branch_is_removed_drop_drop_if_needed() {
            ExpertBuildConfiguration buildConfiguration = new ExpertBuildConfiguration("Main") {
                ModuleName = "Foo",
                DropLocation = @"\\aderant.com\ExpertSuite\Main"
            };

            ExpertBuildDetail detail = new ExpertBuildDetail("99.99.99.99", "1.0.0.0", buildConfiguration);

            Assert.AreEqual(@"\\aderant.com\ExpertSuite\Main\Foo\99.99.99.99\1.0.0.0", detail.DropLocation);
        }
    }
}