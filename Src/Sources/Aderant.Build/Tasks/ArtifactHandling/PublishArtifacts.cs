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

        public ITaskItem[] AdditionalArtifacts { get; set; }

        [Output]
        public string[] LinkCommands { get; private set; }

        [Output]
        public TaskItem[] ArtifactPaths { get; private set; }

        public override bool ExecuteTask() {
            var additionalArtifacts = ArtifactPackageHelper.MaterializeArtifactPackages(AdditionalArtifacts, null);

            var service = new ArtifactService(PipelineService, null, Logger);
            var commands = service.CreateLinkCommands(ArtifactStagingDirectory, DestinationRootPath, additionalArtifacts);

            LinkCommands = commands.AssociationCommands.ToArray();
            ArtifactPaths = commands.ArtifactPaths.Select(s => new TaskItem(s.Location, new Hashtable { { "TargetPath", s.Destination } })).ToArray();

            return !Log.HasLoggedErrors;
        }
    }

}
