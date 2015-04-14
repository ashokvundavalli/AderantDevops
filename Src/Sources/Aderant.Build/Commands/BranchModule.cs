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
       
        [Parameter(Position = 0, HelpMessage = "The name of the module to branch")]
        public string ModuleName {
            get;
            set;
        }

        [Parameter(Position = 1, HelpMessage = "The name of the source branch e.g. Main")]
        public string SourceBranch {
            get;
            set;
        }

        [Parameter(Position = 2, HelpMessage = @"The name of the target branch e.g. Dev\OnTheGo")]
        public string TargetBranch {
            get;
            set;
        }

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
            TfsTeamProjectCollection collection = TeamFoundationHelper.GetTeamProjectServer();

            string serverPathToModule;
            Workspace wss = Branch(collection, out serverPathToModule);

            GetBuildInfrastructure(wss, Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), "Build.Infrastructure"));

            BuildInfrastructureHelper.UpdatePathToModuleBuildProject(wss, serverPathToModule, ParameterHelper.GetDropPath(TargetBranch, SessionState));

            GetBuildFilesForAllModules(wss);

            UpdateBuildAll(wss);
        }

        private static void GetBuildInfrastructure(Workspace wss, string combine) {
            ItemSet files = wss.VersionControlServer.GetItems(
                new ItemSpec(combine,
                             RecursionType.Full),
                VersionSpec.Latest,
                DeletedState.NonDeleted,
                ItemType.Any,
                false);

            wss.Get(files.Items.Select(s => s.ServerItem).ToArray(), VersionSpec.Latest, RecursionType.Full, GetOptions.None);
        }

        private Workspace Branch(TfsTeamProjectCollection collection, out string fullTargetServerPath) {
            string fullSourceServerPath = Path.Combine(PathHelper.GetServerPathToModuleDirectory(SourceBranch), ModuleName);
            fullTargetServerPath = Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), ModuleName);

            Workspace workspace = null;

            var workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();
            foreach (WorkspaceInfo info in workspaceInfo) {
                workspace = info.GetWorkspace(collection);

                string localSourcePathForModule = workspace.TryGetLocalItemForServerItem(fullSourceServerPath);
                if (!string.IsNullOrEmpty(localSourcePathForModule)) {
                    break;
                }
            }

            if (workspace != null) {
                // Branch the module
                Host.UI.WriteLine("Branching module: " + ModuleName);
                workspace.PendBranch(fullSourceServerPath, fullTargetServerPath, VersionSpec.Latest);

                return workspace;
            }
            return null;
        }

        private void GetBuildFilesForAllModules(Workspace wss) {
            // Get and checkout ExpertManifest
            string serverPathToManifest = Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), PathHelper.PathToProductManifest);

            Host.UI.WriteLine("Getting latest: " + serverPathToManifest);
            wss.Get(new string[] {serverPathToManifest}, VersionSpec.Latest, RecursionType.None, GetOptions.None);

            Host.UI.WriteLine("Checking out: " + serverPathToManifest);
            wss.PendEdit(serverPathToManifest);

            string root = Providers.PathHelper.GetServerPathToModuleDirectory(TargetBranch) + @"/*";

            Host.UI.WriteLine("Getting build system files for each module in branch: " + TargetBranch);

            // Grab the latest core dependency files so the Expert Manifest builder can construct a new manifest for the branch
            ItemSet folders = wss.VersionControlServer.GetItems(
                new ItemSpec(root,
                             RecursionType.OneLevel),
                VersionSpec.Latest,
                DeletedState.NonDeleted,
                ItemType.Folder,
                false);

            foreach (Item folder in folders.Items) {
                ItemSet itemSet = wss.VersionControlServer.GetItems(
                    new ItemSpec(folder.ServerItem + "/Build",
                                 RecursionType.Full),
                    VersionSpec.Latest,
                    DeletedState.NonDeleted,
                    ItemType.Any, false);

                foreach (var item in itemSet.Items) {
                    Host.UI.WriteLine("Getting: " + item.ServerItem);

                    if (item.ItemType == ItemType.Folder) {
                        wss.Get(new string[] {item.ServerItem}, VersionSpec.Latest, RecursionType.Full, GetOptions.None);
                    }

                    GetStatus status = wss.Get(new string[] {item.ServerItem + "/*"}, VersionSpec.Latest, RecursionType.Full, GetOptions.None);
                }
            }

            Host.UI.WriteLine();
            Host.UI.WriteLine();
            Host.UI.WriteLine("Updating product manifest");

            string moduleDirectory = wss.TryGetLocalItemForServerItem(PathHelper.GetServerPathToModuleDirectory(TargetBranch));
            ProductManifestUpdater updater = new ProductManifestUpdater(new PowerShellLogger(Host), ExpertManifest.Load(moduleDirectory));
            updater.Update(SourceBranch, TargetBranch);
        }

        private void UpdateBuildAll(Workspace wss) {
            string serverPathToBuildProject = Path.Combine(PathHelper.GetServerPathToModuleDirectory(TargetBranch), PathHelper.PathToBuildOrderProject);

            wss.Get(new string[] {serverPathToBuildProject}, VersionSpec.Latest, RecursionType.None, GetOptions.None);

            string localProject = wss.TryGetLocalItemForServerItem(serverPathToBuildProject);

            if (!string.IsNullOrEmpty(localProject)) {
                int pendEdit = wss.PendEdit(localProject);

                if (pendEdit > 0) {
                    Host.UI.WriteLine();
                    Host.UI.WriteLine();
                    Host.UI.WriteLine("Updating branch Modules.proj");

                    string localPath = wss.TryGetLocalItemForServerItem(PathHelper.GetServerPathToModuleDirectory(TargetBranch));

                    string sequence = BuildProjectSequencer.CreateOrUpdateBuildSequence(wss, localProject, ExpertManifest.Load(localPath));
                    File.WriteAllText(localProject, sequence);
                }
            }
        }
    }
}