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

            Log.LogMessage("Finalizing build: {0} (Current status: {1})", BuildToFinalizeUri, currentBuild.Status);

            // If the current build is still running then we assume that the build should be marked as Completed.
            controller.FinalizeBuild(buildToFinalize, currentBuild.Status == BuildStatus.InProgress);

            return !Log.HasLoggedErrors;
        }
    }
}