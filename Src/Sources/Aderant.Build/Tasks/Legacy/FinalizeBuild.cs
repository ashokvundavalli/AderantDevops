using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Client;

namespace Aderant.Build.Tasks {
    public sealed class FinalizeBuild : Task {
        /// <summary>
        /// Gets or sets the team foundation server URI.
        /// </summary>
        /// <value>
        /// The team foundation server URI.
        /// </value>
        [Required]
        public string TeamFoundationServerUri { get; set; }

        /// <summary>
        /// Gets or sets the team project.
        /// </summary>
        /// <value>
        /// The team project.
        /// </value>
        [Required]
        public string TeamProject { get; set; }

        /// <summary>
        /// Gets or sets the build URI.
        /// </summary>
        /// <value>
        /// The build URI.
        /// </value>
        [Required]
        public string BuildToFinalizeUri { get; set; }

        [Required]
        public string CurrentBuildUri { get; set; }

        /// <summary>
        /// Gets or sets the name of the module.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        [Required]
        public string ModuleName { get; set; }

        public override bool Execute() {
            BuildDetailPublisher controller = new BuildDetailPublisher(TeamFoundationServerUri, TeamProject);
            IBuildDetail currentBuild = controller.GetBuildDetails(CurrentBuildUri);
            IBuildDetail buildToFinalize = controller.GetBuildDetails(BuildToFinalizeUri);

            Log.LogMessage("Finalizing build: {0}. Status: {1} CompilationStatus: {2} TestStatus: {3}.", BuildToFinalizeUri, currentBuild.Status, currentBuild.CompilationStatus, currentBuild.TestStatus);

            bool isSuccessful = IsSuccessful(currentBuild);

            controller.FinalizeBuild(buildToFinalize, isSuccessful);

            return !Log.HasLoggedErrors;
        }

        private bool IsSuccessful(IBuildDetail currentBuild) {
            if (currentBuild.Status == BuildStatus.InProgress) {
                if (currentBuild.CompilationStatus == BuildPhaseStatus.Failed) {
                    return false;
                } 

                if (currentBuild.TestStatus == BuildPhaseStatus.Failed) {
                    return false;
                }
            }

            if (currentBuild.Status == BuildStatus.Failed) {
                return false;
            }

            if (currentBuild.Status == BuildStatus.InProgress) {
                return true;
            }

            // Assume all is green?
            return true;
        }
    }
}