using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Packaging {
    internal static class ArtifactPackageHelper {
        internal static List<ArtifactPackageDefinition> MaterializeArtifactPackages(ITaskItem[] artifactDefinitions, string[] relativeFrom) {
            List<ArtifactPackageDefinition> artifacts = new List<ArtifactPackageDefinition>();
            var grouping = artifactDefinitions.GroupBy(g => g.GetMetadata("ArtifactId"), StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouping) {
                List<PathSpec> pathSpecs = new List<PathSpec>();

                bool isAutomaticallyGenerated = false;
                bool isInternalDevelopmentPackage = false;
                ArtifactType artifactType = ArtifactType.None;

                foreach (var file in group) {
                    ParseMetadata(file, "Generated", ref isAutomaticallyGenerated);
                    ParseMetadata(file, "IsInternalDevelopmentPackage", ref isInternalDevelopmentPackage);
                    ParseMetadata(file, "ArtifactType", ref artifactType);

                    var pathSpec = ArtifactPackageDefinition.CreatePathSpecification(
                        relativeFrom,
                        file.GetMetadata("FullPath"),
                        file.GetMetadata("TargetPath") // The destination location (assumed to be relative to "RelativeFrom")
                    );

                    if (!pathSpecs.Contains(pathSpec)) {
                        pathSpecs.Add(pathSpec);
                    }
                }

                var artifact = new ArtifactPackageDefinition(group.Key, pathSpecs) {
                    IsAutomaticallyGenerated = isAutomaticallyGenerated,
                    IsInternalDevelopmentPackage = isInternalDevelopmentPackage,
                    ArtifactType = artifactType,
                };

                artifacts.Add(artifact);
            }

            return artifacts;
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
