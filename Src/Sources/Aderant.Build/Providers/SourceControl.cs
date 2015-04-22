using System.IO;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Providers {
    internal class SourceControl {
        private readonly string branchRoot;
        private Workspace workspace;

        private SourceControl(Workspace workspace, string branchRoot) {
            this.branchRoot = branchRoot;
            this.workspace = workspace;
            
            BranchInfo = new BranchInfo();

            string thirdparty = Path.Combine(branchRoot, "ThirdParty");
            if (Directory.Exists(thirdparty)) {
                BranchInfo.ThirdPartyFolder = thirdparty;
            }
        }

        /// <summary>
        /// Gets the branch information.
        /// </summary>
        /// <value>
        /// The branch information.
        /// </value>
        public BranchInfo BranchInfo { get; private set; }

        public static SourceControl CreateFromBranchRoot(string path) {
            var workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();
            foreach (WorkspaceInfo info in workspaceInfo) {

                // If the "tfs/ADERANT" collection is not found inside of the current WorkspaceInfo object, then move to the next
                Workspace workspace;
                try {
                    workspace = info.GetWorkspace(TeamFoundationHelper.GetTeamProjectServer());
                } catch (System.InvalidOperationException ex) {
                    continue;
                }

                string serverPathToModule = workspace.TryGetServerItemForLocalItem(path);

                if (!string.IsNullOrEmpty(serverPathToModule)) {
                    return new SourceControl(workspace, path);
                }
            }

            return null;
        }
    }
}