using System;
using System.Collections.Generic;
using Aderant.Build.PipelineService;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Packaging {
    internal class ArtifactTargetPathAssigner {

        private readonly IBuildPipelineService pipelineService;

        public ArtifactTargetPathAssigner(IBuildPipelineService pipelineService) {
            this.pipelineService = pipelineService;
        }

        public IDictionary<string, List<BuildArtifact>> Process(bool includeGeneratedArtifacts) {
            BuildArtifact[] associatedArtifacts = pipelineService.GetAssociatedArtifacts();

            // Sorted for determinism.
            var pathMap = new SortedDictionary<string, List<BuildArtifact>>(StringComparer.OrdinalIgnoreCase);

            foreach (BuildArtifact artifact in associatedArtifacts) {
                bool deliverToRoot = true;

                if (artifact.PackageType.Contains(ArtifactPackageType.DevelopmentPackage)) {
                    AddArtifact(pathMap, "Development", artifact);
                    deliverToRoot = false;
                }

                if (artifact.PackageType.Contains(ArtifactPackageType.TestPackage)) {
                    AddArtifact(pathMap, "Test", artifact);
                    deliverToRoot = false;
                }

                if (artifact.PackageType.Contains(ArtifactPackageType.AutomationPackage)) {
                    AddArtifact(pathMap, @"Test\Automation", artifact);
                    deliverToRoot = false;
                }

                if (!deliverToRoot) {
                    if (!artifact.PackageType.Contains(ArtifactPackageType.AutomationPackage)) {
                        continue;
                    }
                    deliverToRoot = true;
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

        private static void AddArtifact(IDictionary<string, List<BuildArtifact>> pathMap, string destinationSubDirectory, BuildArtifact artifact) {
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
