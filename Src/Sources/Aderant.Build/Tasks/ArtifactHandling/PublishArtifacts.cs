using System.Collections;
using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.ArtifactHandling {

    public sealed class PublishArtifacts : BuildOperationContextTask {

        [Required]
        public string DestinationRootPath { get; set; }

        public string ArtifactStagingDirectory { get; set; }

        [Required]
        public ITaskItem[] AdditionalArtifacts { get; set; }

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
            System.Diagnostics.Debugger.Launch();

            var additionalArtifacts = ArtifactPackageHelper.MaterializeArtifactPackages(AdditionalArtifacts, null);

            var service = new ArtifactService(PipelineService, null, Logger);
            var commands = service.CreateLinkCommands(ArtifactStagingDirectory, Context.DropLocationInfo, Context.BuildMetadata, additionalArtifacts);

            LinkCommands = commands.AssociationCommands.ToArray();
            ArtifactPaths = commands.ArtifactPaths.Select(s => new TaskItem(s.Location, new Hashtable { { "TargetPath", s.Destination } })).ToArray();

            return !Log.HasLoggedErrors;
        }
    }

}
