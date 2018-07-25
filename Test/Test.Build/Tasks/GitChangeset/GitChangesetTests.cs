using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GitChangeset {
    [TestClass]
    public class GitChangesetTests : BuildTaskTestBase {

        [TestMethod]
        public void GitChangesetExecuteTest() {
            var testContextDeploymentDirectory = TestContext.TestRunDirectory;

            var changeset = new ChangesetResolver(null, testContextDeploymentDirectory);
            Assert.IsNotNull(changeset.ChangedFiles);
        }
    }
}
