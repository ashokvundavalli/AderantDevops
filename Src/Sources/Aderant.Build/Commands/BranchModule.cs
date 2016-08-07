using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;
using Lapointe.PowerShell.MamlGenerator.Attributes;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Commands {
    [CmdletDescription(@"Branches the TFS source from TFS into the relevant path in the target branch location.
Updates the ExpertManifest to get the branched module from this branch
Checking back in to TFS must be done manually.")]
    [Cmdlet("Branch", "Module")]
    public class BranchModule : PSCmdlet {
        private VersionControlServer versionControl;
        private ITeamFoundationWorkspace workspace;

        [Parameter(Position = 0, HelpMessage = "The name of the module to branch")]
        public string ModuleName { get; set; }

        [Parameter(Position = 1, HelpMessage = "The name of the source branch e.g. Main")]
        public string SourceBranch { get; set; }

        [Parameter(Position = 2, HelpMessage = @"The name of the target branch e.g. Dev\OnTheGo")]
        public string TargetBranch { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (string.IsNullOrEmpty(ModuleName)) {
                Host.UI.WriteErrorLine("No module specified.");
                return;
            }

            if (string.IsNullOrEmpty(SourceBranch)) {
                Host.UI.WriteErrorLine("No source branch specified.");
                return;
            }

            if (string.IsNullOrEmpty(TargetBranch)) {
                Host.UI.WriteErrorLine("No target branch specified.");
                return;
            }

            this.versionControl = ServiceLocator.GetInstance<VersionControlServer>();
            this.workspace = ServiceLocator.GetInstance<ITeamFoundationWorkspace>();

            Host.UI.WriteLine("Module: ".PadRight(30) + ModuleName);
            Host.UI.WriteLine("Source Branch: ".PadRight(30) + SourceBranch);
            Host.UI.WriteLine("Target Branch: ".PadRight(30) + TargetBranch);

            Collection<ChoiceDescription> descriptions = new Collection<ChoiceDescription>(
                new[] {
                    new ChoiceDescription("Y"),
                    new ChoiceDescription("N")
                });

            int choice = Host.UI.PromptForChoice("Branch module", "Do you run the branch operation?", descriptions, 0);

            if (choice != 0) {
                return;
            }

            ExecuteBranchModule();

            Host.UI.WriteLine(new string('=', 40));
            Host.UI.WriteLine("Branch Complete");
            Host.UI.WriteLine(new string('=', 40));
        }


        private void ExecuteBranchModule() {
            Branch();

            GetBuildInfrastructure(Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), "Build.Infrastructure"));

            GetBuildFilesForAllModules();

            UpdateBuildAll();
        }

        private void GetBuildInfrastructure(string combine) {
            ItemSet files = versionControl.GetItems(
                new ItemSpec(combine,
                    RecursionType.Full),
                VersionSpec.Latest,
                DeletedState.NonDeleted,
                ItemType.Any,
                false);

            workspace.Get(files.Items.Select(s => s.ServerItem).ToArray(), VersionSpec.Latest, RecursionType.Full, GetOptions.None);
        }

        private void Branch() {
            string fullSourceServerPath = Path.Combine(PathHelper.GetServerPathToModuleDirectory(SourceBranch), ModuleName);
            string fullTargetServerPath = Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), ModuleName);

            // Branch the module
            Host.UI.WriteLine("Branching module: " + ModuleName);
            workspace.PendBranch(fullSourceServerPath, fullTargetServerPath, VersionSpec.Latest);
        }

        private void GetBuildFilesForAllModules() {
            // Get and checkout ExpertManifest
            string serverPathToManifest = Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), "ExpertManifest.xml");

            Host.UI.WriteLine("Getting latest: " + serverPathToManifest);
            workspace.Get(new string[] {serverPathToManifest}, VersionSpec.Latest, RecursionType.None, GetOptions.None);

            Host.UI.WriteLine("Checking out: " + serverPathToManifest);
            workspace.PendEdit(serverPathToManifest);

            string root = PathHelper.GetServerPathToModuleDirectory(TargetBranch) + @"/*";

            Host.UI.WriteLine("Getting build system files for each module in branch: " + TargetBranch);

            // Grab the latest core dependency files so the Expert Manifest builder can construct a new manifest for the branch
            ItemSet folders = versionControl.GetItems(
                new ItemSpec(root,
                    RecursionType.OneLevel),
                VersionSpec.Latest,
                DeletedState.NonDeleted,
                ItemType.Folder,
                false);

            foreach (Item folder in folders.Items) {
                ItemSet itemSet = versionControl.GetItems(
                    new ItemSpec(folder.ServerItem + "/Build",
                        RecursionType.Full),
                    VersionSpec.Latest,
                    DeletedState.NonDeleted,
                    ItemType.Any, false);

                foreach (var item in itemSet.Items) {
                    Host.UI.WriteLine("Getting: " + item.ServerItem);

                    if (item.ItemType == ItemType.Folder) {
                        workspace.Get(new string[] {item.ServerItem}, VersionSpec.Latest, RecursionType.Full, GetOptions.None);
                    }

                    workspace.Get(new string[] {item.ServerItem + "/*"}, VersionSpec.Latest, RecursionType.Full, GetOptions.None);
                }
            }

            Host.UI.WriteLine();
            Host.UI.WriteLine();
            Host.UI.WriteLine("Updating product manifest");

            string moduleDirectory = workspace.TryGetLocalItemForServerItem(PathHelper.GetServerPathToModuleDirectory(TargetBranch));
            ProductManifestUpdater updater = new ProductManifestUpdater(new PowerShellLogger(Host), ExpertManifest.Load(moduleDirectory));
            updater.Update(SourceBranch, TargetBranch);
        }

        private void UpdateBuildAll() {
            string serverPathToBuildProject = Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), PathHelper.PathToBuildOrderProject);

            workspace.Get(new string[] {serverPathToBuildProject}, VersionSpec.Latest, RecursionType.None, GetOptions.None);

            string localProject = workspace.TryGetLocalItemForServerItem(serverPathToBuildProject);

            if (!string.IsNullOrEmpty(localProject)) {
                workspace.PendEdit(localProject);

                Host.UI.WriteLine();
                Host.UI.WriteLine();
                Host.UI.WriteLine("Updating branch Modules.proj");

                string localPath = workspace.TryGetLocalItemForServerItem(PathHelper.GetServerPathToModuleDirectory(TargetBranch));

                string sequence = BuildProjectSequencer.CreateOrUpdateBuildSequence(workspace, localProject, ExpertManifest.Load(localPath));
                File.WriteAllText(localProject, sequence);
            }
        }
    }
}