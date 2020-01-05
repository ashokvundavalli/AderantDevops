using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Aderant.Build.Packaging {
    internal static class ArtifactPackageHelper {
        /// <summary>
        /// Creates a definition from a bag of task items.
        /// </summary>
        /// <param name="artifactDefinitions">The artifact definitions.</param>
        /// <param name="relativeFrom">The rooting directories to create relative paths from.</param>
        /// <param name="includeDirectoryPathsOnly">
        /// if set to <c>true</c> then only directories paths will be added to the artifact
        /// definition instead of files.
        /// </param>
        internal static List<ArtifactPackageDefinition> MaterializeArtifactPackages(ITaskItem[] artifactDefinitions, string[] relativeFrom, bool includeDirectoryPathsOnly) {
            Dictionary<string, List<ITaskItem>> artifactMap = BuildOrderedArtifactPackageDefinitionList(artifactDefinitions);

            List<ArtifactPackageDefinition> artifacts = new List<ArtifactPackageDefinition>();
            HashSet<string> claimedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in artifactMap) {
                List<PathSpec> pathSpecs = new List<PathSpec>();

                bool isDeliveringToRoot = false;
                bool isAutomaticallyGenerated = false;
                bool isInternalDevelopmentPackage = false;
                bool isAutomationPackage = false;
                bool isTestPackage = false;
                ArtifactType artifactType = ArtifactType.None;

                foreach (var file in group.Value) {
                    ParseMetadata(file, "Generated", ref isAutomaticallyGenerated);
                    ParseMetadata(file, "DeliverToRoot", ref isDeliveringToRoot);
                    ParseMetadata(file, "IsInternalDevelopmentPackage", ref isInternalDevelopmentPackage);
                    ParseMetadata(file, "IsAutomationPackage", ref isAutomationPackage);
                    ParseMetadata(file, "IsTestPackage", ref isTestPackage);
                    ParseMetadata(file, "ArtifactType", ref artifactType);

                    PathSpec pathSpec;
                    if (!includeDirectoryPathsOnly) {
                        pathSpec = ArtifactPackageDefinition.CreatePathSpecification(
                            relativeFrom,
                            file.GetMetadata("FullPath"),
                            file.GetMetadata("TargetPath") // The destination location (assumed to be relative to "RelativeFrom")
                        );
                    } else {
                        pathSpec = ArtifactPackageDefinition.CreatePathSpecification(
                            relativeFrom,
                            file.GetMetadata("RootDir") + file.GetMetadata("Directory"),
                            file.GetMetadata("TargetPath")
                        );
                    }

                    // If this is an auto-package but a custom package has claimed the file so do not double claim it
                    bool claimFile = true;

                    if (isAutomaticallyGenerated) {
                        if (claimedFiles.Contains(pathSpec.Location)) {
                            claimFile = false;
                        }
                    } else {
                        claimedFiles.Add(pathSpec.Location);
                    }

                    if (claimFile) {
                        if (!pathSpecs.Contains(pathSpec)) {
                            pathSpecs.Add(pathSpec);
                        }
                    }
                }

                HashSet<ArtifactPackageType> packageType = new HashSet<ArtifactPackageType>();
                packageType.Add(ArtifactPackageType.Default);
                
                if (isInternalDevelopmentPackage) {
                    packageType.Add(ArtifactPackageType.DevelopmentPackage);
                }

                if (isTestPackage || group.Key.IndexOf("IntegrationTest", StringComparison.OrdinalIgnoreCase) >= 0 || group.Key.EndsWith(".tests", StringComparison.OrdinalIgnoreCase)) {
                    packageType.Add(ArtifactPackageType.TestPackage);
                } 

                if (isAutomationPackage) {
                    packageType.Add(ArtifactPackageType.AutomationPackage);
                }

                if (isDeliveringToRoot) {
                    packageType.Add(ArtifactPackageType.DeliverToRoot);
                }

                var artifact = new ArtifactPackageDefinition(group.Key, pathSpecs) {
                    IsAutomaticallyGenerated = isAutomaticallyGenerated,
                    PackageType = packageType,
                    ArtifactType = artifactType
                };

                if (artifact.GetFiles().Count > 0) {
                    artifacts.Add(artifact);
                }
            }

            return artifacts;
        }

        private static Dictionary<string, List<ITaskItem>> BuildOrderedArtifactPackageDefinitionList(ITaskItem[] artifactDefinitions) {
            List<ITaskItem> nonCustomArtifactFiles = new List<ITaskItem>();
            List<ITaskItem> customArtifactFiles = new List<ITaskItem>();

            // Pull out the generated flag for all items, partition the items into two sets.
            foreach (var item in artifactDefinitions) {
                var metadata = item.GetMetadata("Generated");

                if (string.Equals("true", metadata, StringComparison.OrdinalIgnoreCase)) {
                    nonCustomArtifactFiles.Add(item);
                } else {
                    customArtifactFiles.Add(item);
                }
            }

            var artifactMap = new Dictionary<string, List<ITaskItem>>(StringComparer.InvariantCultureIgnoreCase);

            // Concatenate the two sequences, this gives us an ordering where custom artifact files are first.
            // This ordering is exploited in the file processor to ensure that custom file assignment wins over default file assignment
            var files = new List<ITaskItem>();
            files.AddRange(customArtifactFiles);
            files.AddRange(nonCustomArtifactFiles);

            foreach (var file in files) {
                var id = file.GetMetadata("ArtifactId");

                if (string.IsNullOrEmpty(id)) {
                    continue;
                }

                List<ITaskItem> items;
                if (!artifactMap.TryGetValue(id, out items)) {
                    items = new List<ITaskItem>();
                    artifactMap[id] = items;
                }

                items.Add(file);
            }

            return artifactMap;
        }

        private static void ParseMetadata(ITaskItem file, string metadataName, ref bool value) {
            string metadata = file.GetMetadata(metadataName);
            if (!string.IsNullOrWhiteSpace(metadata)) {
                value = bool.Parse(metadata);
            }
        }

        internal static void ParseMetadata(ITaskItem file, string metadataName, ref ArtifactType value) {
            string metadata = file.GetMetadata(metadataName);
            if (!string.IsNullOrWhiteSpace(metadata)) {
                string[] parts = metadata.Split('|');

                ArtifactType output;
                foreach (var part in parts) {
                    if (Enum.TryParse(part, true, out output)) {
                        value = value | output;
                    }
                }
            }
        }
    }

    [Flags]
    internal enum ArtifactType {
        None = 0,
        Prebuilt = 2,
        Branch = 4,
    }
}
