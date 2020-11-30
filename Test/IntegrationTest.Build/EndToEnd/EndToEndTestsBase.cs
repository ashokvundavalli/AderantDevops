using IntegrationTest.Build.Helpers;

namespace IntegrationTest.Build.EndToEnd {
    public abstract class EndToEndTestsBase : MSBuildIntegrationTestBase {

        protected abstract string DeploymentItemsDirectory { get; }

        protected void CleanWorkingDirectory() {
            PowerShellHelper.RunCommand("& git clean -fdx", TestContext, DeploymentItemsDirectory);
        }

        protected void CommitChanges() {
            PowerShellHelper.RunCommand("& git add .", TestContext, DeploymentItemsDirectory);
            PowerShellHelper.RunCommand("& git commit -m \"Add\"", TestContext, DeploymentItemsDirectory);
        }

        protected void AddFilesToNewGitRepository() {
            PowerShellHelper.RunCommand(Resources.CreateRepo, TestContext, DeploymentItemsDirectory);
        }

    }
}
