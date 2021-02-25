using System.Collections.Generic;
using System.IO;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.ArtifactHandling {
    public sealed class CreateArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        public string[] RelativeFrom { get; set; }

        public ITaskItem[] ArtifactDefinitions { get; set; }

        public override bool ExecuteTask() {
            if (ArtifactDefinitions != null) {
                List<ArtifactPackageDefinition> artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, RelativeFrom, false);

                ArtifactService artifactService = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
                var context = PipelineService.GetContext(new QueryOptions {
                    IncludeStateFiles = false,
                    IncludeBuildMetadata = true,
                    IncludeSourceTreeMetadata = true
                });

                artifactService.CreateArtifacts(context, Path.GetFileName(SolutionRoot), artifacts);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
