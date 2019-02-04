using System.IO;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;

namespace Aderant.Build.Commands {

    [Cmdlet("Push", "PaketTemplates")]
    public class PushPaketTemplates : PSCmdlet {
        private ILogger logger;

        protected override void ProcessRecord() {
            this.logger = new PowerShellLogger(Host);

            var branchModulesDirectory = ParameterHelper.GetBranchModulesDirectory(null, this.SessionState);
            var branchName = ParameterHelper.GetBranchName(this.SessionState);

            var buildScriptsDirectory = Path.Combine(branchModulesDirectory, BuildInfrastructureHelper.PathToBuildScriptsFromModules);
            var rootFolder = BuildInfrastructureHelper.GetPathToThirdPartyModules(branchModulesDirectory, branchName);

            var action = new PushPaketTemplatesAction();
            action.Execute(logger, buildScriptsDirectory, rootFolder);
        }
    }
}