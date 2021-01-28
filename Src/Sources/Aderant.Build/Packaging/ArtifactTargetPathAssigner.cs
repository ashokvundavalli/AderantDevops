using System;
using System.Collections.Generic;
using Aderant.Build.PipelineService;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Packaging {
    internal class ArtifactTargetPathAssigner {

        private readonly IBuildPipelineService pipelineService;
        internal static readonly string DestinationSubDirectory = "DestinationSubDirectory";

        internal string IntegrationTestDirectory = string.Empty;
        internal string AutomatedTestDirectory = string.Empty;

        public ArtifactTargetPathAssigner(IBuildPipelineService pipelineService) {
            this.pipelineService = pipelineService;
        }

        public IDictionary<string, List<(BuildArtifact, ArtifactPackageType)>> Process(bool includeGeneratedArtifacts) {
            BuildArtifact[] associatedArtifacts = pipelineService.GetAssociatedArtifacts();

            // Sorted for determinism.
            var pathMap = new SortedDictionary<string, List<(BuildArtifact, ArtifactPackageType)>>(StringComparer.OrdinalIgnoreCase);

            foreach (BuildArtifact artifact in associatedArtifacts) {
                bool deliverToRoot = true;

                if (artifact.PackageType.Contains(ArtifactPackageType.DevelopmentPackage)) {
                    AddArtifact(pathMap, "Development", artifact, ArtifactPackageType.DevelopmentPackage);
                    deliverToRoot = false;
                }

                if (artifact.PackageType.Contains(ArtifactPackageType.TestPackage)) {
                    AddArtifact(pathMap, IntegrationTestDirectory, artifact, ArtifactPackageType.TestPackage);
                    deliverToRoot = false;
                }

                if (artifact.PackageType.Contains(ArtifactPackageType.AutomationPackage)) {
                    string destination;

                    if (!AutomatedTestDirectory.EndsWith(@"Automation\", StringComparison.OrdinalIgnoreCase)) {
                        // Ensure artifacts not packaged within the Automation sub-directory are copied to the appropriate location.
                        destination = artifact.Name.IndexOf("AutomationTest", StringComparison.OrdinalIgnoreCase) == -1 ? string.Concat(AutomatedTestDirectory, @"Automation\") : AutomatedTestDirectory;
                    } else {
                        // Default case for backwards compatibility.
                        destination = AutomatedTestDirectory;
                    }

                    AddArtifact(pathMap, destination, artifact, ArtifactPackageType.AutomationPackage);
                    deliverToRoot = false;
                }

                if (!deliverToRoot) {
                    if (!artifact.PackageType.Contains(ArtifactPackageType.AutomationPackage)) {
                        continue;
                    }
                    deliverToRoot = true;
                }

                if (includeGeneratedArtifacts) {
                    AddArtifact(pathMap, string.Empty, artifact, ArtifactPackageType.Default);
                } else {
                    if (!artifact.IsAutomaticallyGenerated) {
                        AddArtifact(pathMap, string.Empty, artifact, ArtifactPackageType.Default);
                    }
                }
            }

            return pathMap;
        }

        private static void AddArtifact(IDictionary<string, List<(BuildArtifact, ArtifactPackageType)>> pathMap, string destinationSubDirectory, BuildArtifact artifact, ArtifactPackageType artifactPackageType) {
            List<(BuildArtifact, ArtifactPackageType)> list;
            if (!pathMap.TryGetValue(destinationSubDirectory, out list)) {
                list = new List<(BuildArtifact, ArtifactPackageType)>();
                pathMap[destinationSubDirectory] = list;
            }

            list.Add((artifact, artifactPackageType));
        }

        public IEnumerable<ITaskItem> CreateTaskItemsWithTargetPaths(bool includeGeneratedArtifacts) {
            var map = Process(includeGeneratedArtifacts);

            foreach (var item in map) {
                foreach (var path in item.Value) {
                    var taskItem = new TaskItem(path.Item1.SourcePath);
                    taskItem.SetMetadata(DestinationSubDirectory, PathUtility.EnsureTrailingSlash(item.Key));
                    taskItem.SetMetadata(nameof(ArtifactPackageType), path.Item2.ToString());

                    yield return taskItem;
                }
            }
        }
    }
}
