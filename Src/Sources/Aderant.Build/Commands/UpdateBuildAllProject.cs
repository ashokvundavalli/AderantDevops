using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Commands {
    /// <summary>
    /// Handles the creation of a MS Build project file to control the build process.
    /// </summary>
    [Cmdlet(VerbsData.Update, "BuildAllProject")]
    public sealed class UpdateBuildAllProject : PSCmdlet {
        /// <summary>
        /// Gets or sets the path to the module project.
        /// </summary>
        /// <value>
        /// The module project.
        /// </value>
        [Parameter(Mandatory = false, Position = 0, HelpMessage = "Specifies the path to a MS Build project containing a single item group which specifies the branch build configuration.")]
        public string ModuleProject { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchPath(Path.GetDirectoryName(ModuleProject), SessionState);

            if (string.IsNullOrEmpty(ModuleProject)) {
                ModuleProject = Path.Combine(branchPath, Path.Combine("Modules", "Modules.proj"));
            }

            if (!string.IsNullOrEmpty(ModuleProject)) {
                Collection<ChoiceDescription> descriptions = new Collection<ChoiceDescription>(
                    new[] {new ChoiceDescription("Y"), new ChoiceDescription("N")});

                int choice = 0;
                if (!System.Diagnostics.Debugger.IsAttached) {
                    choice = Host.UI.PromptForChoice("Update file?", string.Format("Do you want to update: {0} with the new module build order?", ModuleProject), descriptions, 0);
                }

                if (choice == 0) {
                    string moduleDirectory = ParameterHelper.GetBranchModulesDirectory(null, SessionState);
                    IModuleProvider provider = ExpertManifest.Load(moduleDirectory);

                    SequenceBuilds(provider, ModuleProject);
                } else {
                    Host.UI.WriteWarningLine("Creating/updating of project file canceled");
                }
            }
        }

        private void SequenceBuilds(IModuleProvider provider, string moduleProject) {
            TfsTeamProjectCollection teamProject = TeamFoundationHelper.GetTeamProjectServer();

            WorkspaceInfo[] workspaces = Workstation.Current.GetAllLocalWorkspaceInfo();
            foreach (WorkspaceInfo workspaceInfo in workspaces) {

                
                // If the "tfs/ADERANT" collection is not found inside of the current WorkspaceInfo object, then move to the next
                Workspace workspace;
                try {
                    workspace = workspaceInfo.GetWorkspace(teamProject);
                } catch (System.InvalidOperationException) {
                    continue;
                }

                string serverItem = workspace.TryGetServerItemForLocalItem(moduleProject);
                if (!string.IsNullOrEmpty(serverItem)) {
                    Host.UI.WriteLine("Sequencing builds...");

                    string sequence = BuildProjectSequencer.CreateOrUpdateBuildSequence(workspace, moduleProject, provider);

                    Host.UI.WriteLine();
                    Host.UI.WriteLine("Writing updated project file: " + moduleProject);

                    File.WriteAllText(moduleProject, sequence);
                    return;
                }
            }
        }
    }
}