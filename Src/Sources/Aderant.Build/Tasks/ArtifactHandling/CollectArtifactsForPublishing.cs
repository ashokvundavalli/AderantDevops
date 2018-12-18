using System.Collections;
using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.ArtifactHandling {

    /// <summary>
    /// Prepares the item vectors that will be bundled and send to some kind of repository (drop, docker etc)
    /// </summary>
    public sealed class CollectArtifactsForPublishing : BuildOperationContextTask {

        [Required]
        public string DestinationRootPath { get; set; }

        public string ArtifactStagingDirectory { get; set; }

        [Required]
        public ITaskItem[] Artifacts { get; set; }

        public bool AllowNullScmBranch { get; set; }

        /// <summary>
        /// The VSTS artifact association commands
        /// https://github.com/Microsoft/vsts-tasks/blob/master/docs/authoring/commands.md
        /// </summary>
        [Output]
        public string[] LinkCommands { get; private set; }

        /// <summary>
        /// The mapping between source artifacts and storage location
        /// </summary>
        [Output]
        public TaskItem[] ArtifactPaths { get; private set; }

        public override bool ExecuteTask() {
            var additionalArtifacts = ArtifactPackageHelper.MaterializeArtifactPackages(Artifacts, null, true);

            var service = new ArtifactService(PipelineService, null, Logger);
            var commands = service.GetPublishCommands(ArtifactStagingDirectory, Context.DropLocationInfo, Context.BuildMetadata, additionalArtifacts, AllowNullScmBranch);

            LinkCommands = commands.AssociationCommands.ToArray();
            ArtifactPaths = commands.ArtifactPaths.Select(s => new TaskItem(s.Location, new Hashtable { { "TargetPath", s.Destination } })).ToArray();

            return !Log.HasLoggedErrors;
        }
    }

}