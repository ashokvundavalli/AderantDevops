using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.ArtifactHandling {

    public sealed class GetArtifactPaths : BuildOperationContextTask {

        public bool IncludeGeneratedArtifacts { get; set; }

        public string BinariesTestDirectory { get; set; }

        public string IntegrationTestDirectory { get; set; }

        public string AutomatedTestDirectory { get; set; }

        [Output]
        public ITaskItem[] ArtifactPaths { get; private set; }

        public override bool ExecuteTask() {
            var processor = new ArtifactTargetPathAssigner(PipelineService) {
                BinariesTestDirectory = BinariesTestDirectory,
                IntegrationTestDirectory = IntegrationTestDirectory,
                AutomatedTestDirectory = AutomatedTestDirectory
            };

            ArtifactPaths = processor.CreateTaskItemsWithTargetPaths(IncludeGeneratedArtifacts).ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
