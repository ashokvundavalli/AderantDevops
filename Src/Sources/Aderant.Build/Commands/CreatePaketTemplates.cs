using System.IO;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;

namespace Aderant.Build.Commands {

    [Cmdlet("Create", "PaketTemplates")]
    public class CreatePaketTemplates : PSCmdlet {
        protected override void ProcessRecord() {
            var logger = new PowerShellLogger(Host);

            var branchModulesDirectory = ParameterHelper.GetBranchModulesDirectory(null, this.SessionState);
            var branchName = ParameterHelper.GetBranchName(this.SessionState);

            var buildScriptsDirectory = Path.Combine(branchModulesDirectory, BuildInfrastructureHelper.PathToBuildScriptsFromModules);
            var buildToolsDirectory = Path.Combine(branchModulesDirectory, BuildInfrastructureHelper.PathToBuildToolsFromModules);
            var rootFolder = BuildInfrastructureHelper.GetPathToThirdPartyModules(branchModulesDirectory, branchName);

            var action = new CreatePaketTemplatesAction();
            action.Execute(logger, buildScriptsDirectory, buildToolsDirectory, rootFolder);
        }
    }
}
