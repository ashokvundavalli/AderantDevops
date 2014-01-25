using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace DependencyAnalyzer {
    [Cmdlet(VerbsCommon.Set, "ModuleBuildPath")]
    public class ModuleBuildPathUpdateCmd : PSCmdlet {
        [Parameter(HelpMessage = "The module name to update")]
        public string Module { get; set; }

        [Parameter(HelpMessage = "The branch drop location (typically a UNC path)")]
        public string DropPath { get; set; }

        [Parameter(HelpMessage = "Updates all modules within the branch to point to the current drop path")]
        public SwitchParameter AllModulesInBranch { get; set; }

        protected override void BeginProcessing() {
            base.BeginProcessing();

            if (AllModulesInBranch) {
                string branchPath = ParameterHelper.GetBranchPath(null, SessionState);

                DependencyBuilder builder = new DependencyBuilder(branchPath);
                IEnumerable<Build> builds = builder.GetTree(AllModulesInBranch.IsPresent);

                string branchModulesDirectory = ParameterHelper.GetBranchModulesDirectory(null, SessionState);

                foreach (var build in builds) {
                    foreach (var module in build.Modules) {
                        string modulePath = ParameterHelper.GetCurrentModulePath(module.Name, SessionState);
                        modulePath = Path.Combine(branchModulesDirectory, modulePath);
                        UpdatePath(modulePath);
                    }
                }
            } else {
                string modulePath = ParameterHelper.GetCurrentModulePath(Module, SessionState);
                UpdatePath(modulePath);
            }
        }

        private void UpdatePath(string modulePath) {
            var workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();
            foreach (WorkspaceInfo info in workspaceInfo) {
                Workspace workspace = info.GetWorkspace(TeamFoundation.GetTeamProject());

                string serverPathToModule = workspace.TryGetServerItemForLocalItem(modulePath);
                if (!string.IsNullOrEmpty(serverPathToModule)) {
                    BuildInfrastructureHelper.UpdatePathToModuleBuildProject(workspace, serverPathToModule, ParameterHelper.GetDropPath(DropPath ?? string.Empty, SessionState));
                    return;
                }
            }
        }
    }
}