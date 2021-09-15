using IntegrationTest.Build.Helpers;

namespace IntegrationTest.Build.EndToEnd {
    public abstract class EndToEndTestsBase : MSBuildIntegrationTestBase {

        protected abstract string DeploymentItemsDirectory { get; }

        private readonly PowerShellHelper powerShellHelper = new PowerShellHelper();

        protected void CleanWorkingDirectory() {
            powerShellHelper.RunCommand("& git clean -fdx", TestContext, DeploymentItemsDirectory);
        }

        protected void CommitChanges() {
            powerShellHelper.RunCommand("& git add .", TestContext, DeploymentItemsDirectory);
            powerShellHelper.RunCommand("& git commit -m \"Add\"", TestContext, DeploymentItemsDirectory);
        }

        protected void AddFilesToNewGitRepository() {
            powerShellHelper.RunCommand(Resources.CreateRepo, TestContext, DeploymentItemsDirectory);
        }
    }
}
