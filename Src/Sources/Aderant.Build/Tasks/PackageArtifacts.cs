using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.Handlers;
using Aderant.Build.TeamFoundation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public sealed class PublishArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        public string[] RelativeFrom { get; set; }

        public ITaskItem[] ArtifactDefinitions { get; set; }

        public string FileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public override bool ExecuteTask() {
            if (ArtifactDefinitions != null) {
                var artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, SolutionRoot, RelativeFrom);

                var artifactService = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
                artifactService.RegisterHandler(new PullRequestHandler());

                if (!Context.IsDesktopBuild) {
                    artifactService.RegisterHandler(new XamlDropHandler(FileVersion, AssemblyVersion));
                }

                var storageInfo = artifactService.PublishArtifacts(Context, Path.GetFileName(SolutionRoot), artifacts);

                foreach (KeyValuePair<string, ICollection<ArtifactManifest>> pair in Context.GetArtifacts()) {
                    base.PipelineService.RecordArtifacts(pair.Key, pair.Value);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }

    internal static class ArtifactPackageHelper {
        internal static List<ArtifactPackageDefinition> MaterializeArtifactPackages(ITaskItem[] artifactDefinitions, string solutionRoot, string[] relativeFrom) {
            List<ArtifactPackageDefinition> artifacts = new List<ArtifactPackageDefinition>();
            var grouping = artifactDefinitions.GroupBy(g => g.GetMetadata("ArtifactId"));

            foreach (var group in grouping) {
                List<PathSpec> pathSpecs = new List<PathSpec>();

                bool isAutomaticallyGenerated = false;
                bool isInternalDevelopmentPackage = false;

                if (solutionRoot != null) {
                    foreach (var file in group) {
                        ParseMetadata(file, "Generated", ref isAutomaticallyGenerated);
                        ParseMetadata(file, "IsInternalDevelopmentPackage", ref isInternalDevelopmentPackage);

                        var pathSpec = ArtifactPackageDefinition.CreatePathSpecification(
                            solutionRoot,
                            relativeFrom,
                            file.GetMetadata("FullPath"),
                            file.GetMetadata("TargetPath") // The destination location (assumed to be relative to "RelativeFrom")
                        );

                        if (!pathSpecs.Contains(pathSpec)) {
                            pathSpecs.Add(pathSpec);
                        }
                    }
                }

                var artifact = new ArtifactPackageDefinition(group.Key, pathSpecs) {
                    IsAutomaticallyGenerated = isAutomaticallyGenerated,
                    IsInternalDevelopmentPackage = isInternalDevelopmentPackage,
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
    }
}
