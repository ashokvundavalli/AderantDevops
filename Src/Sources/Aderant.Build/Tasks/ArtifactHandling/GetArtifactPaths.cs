using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.ArtifactHandling {

    public sealed class GetArtifactPaths : BuildOperationContextTask {

        public bool IncludeGeneratedArtifacts { get; set; }

        public string BinariesTestDirectory { get; set; }

        [Output]
        public ITaskItem[] ArtifactPaths { get; private set; }

        public override bool ExecuteTask() {
            var processor = new ArtifactTargetPathAssigner(PipelineService) {
                BinariesTestDirectory = BinariesTestDirectory
            };
            ArtifactPaths = processor.CreateTaskItemsWithTargetPaths(IncludeGeneratedArtifacts).ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
