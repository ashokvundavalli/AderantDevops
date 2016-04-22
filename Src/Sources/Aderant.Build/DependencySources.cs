using Aderant.Build.Providers;

namespace Aderant.Build {
    internal class DependencySources {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencySources"/> class.
        /// </summary>
        /// <param name="dropPath">The drop path.</param>
        public DependencySources(string dropPath) {
            DropLocation = dropPath;
        }

        /// <summary>
        /// Gets or sets the third party module location.
        /// </summary>
        /// <value>
        /// The third party.
        /// </value>
        public string LocalThirdPartyDirectory { get; set; }

        /// <summary>
        /// Gets or sets the drop location.
        /// </summary>
        /// <value>
        /// The drop location.
        /// </value>
        public string DropLocation { get; private set; }

        internal static string GetLocalPathToThirdPartyBinaries(string teamProjectServerUri, string branchRoot, string workspaceName, string workspaceOwner) {
            IWorkspace workspace = TeamFoundationWorkspace.Create(teamProjectServerUri, branchRoot, workspaceName, workspaceOwner);

            return workspace.GetThirdPartyFolder();
        }
    }
}