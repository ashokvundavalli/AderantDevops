using System.Collections.Generic;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Packaging {
    internal class ArtifactTargetPathAssigner {

        private readonly IBuildPipelineService pipelineService;

        public ArtifactTargetPathAssigner(IBuildPipelineService pipelineService) {
            this.pipelineService = pipelineService;
        }

        public Dictionary<string, List<BuildArtifact>> Process(bool includeGeneratedArtifacts) {
            BuildArtifact[] associatedArtifacts = pipelineService.GetAssociatedArtifacts();

            var pathMap = new Dictionary<string, List<BuildArtifact>>();

            foreach (BuildArtifact artifact in associatedArtifacts) {
                if (artifact.IsInternalDevelopmentPackage) {
                    AddArtifact(pathMap, "Development", artifact);
                    continue;
                }

                if (artifact.IsTestPackage) {
                    AddArtifact(pathMap, "Test", artifact);
                    continue;
                }

                if (includeGeneratedArtifacts) {
                    AddArtifact(pathMap, "", artifact);
                } else {
                    if (!artifact.IsAutomaticallyGenerated) {
                        AddArtifact(pathMap, "", artifact);
                    }
                }
            }

            return pathMap;
        }

        private static void AddArtifact(Dictionary<string, List<BuildArtifact>> pathMap, string destinationSubDirectory, BuildArtifact artifact) {
            List<BuildArtifact> list;
            if (!pathMap.TryGetValue(destinationSubDirectory, out list)) {
                list = new List<BuildArtifact>();
                pathMap[destinationSubDirectory] = list;
            }

            list.Add(artifact);
        }

        public IEnumerable<TaskItem> CreateTaskItemsWithTargetPaths(bool includeGeneratedArtifacts) {
            var map = Process(includeGeneratedArtifacts);

            foreach (var item in map) {
                foreach (var path in item.Value) {
                    var taskItem = new TaskItem(path.SourcePath);
                    taskItem.SetMetadata("DestinationSubDirectory", PathUtility.EnsureTrailingSlash(item.Key));

                    yield return taskItem;
                }
            }
        }
    }
}
