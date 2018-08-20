using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Packaging;
using Aderant.Build.TeamFoundation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public sealed class PublishArtifacts : BuildOperationContextTask {

        public string SolutionRoot { get; set; }

        public string[] RelativeFrom { get; set; }

        public ITaskItem[] ArtifactDefinitions { get; set; }

        public string FileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        protected override bool UpdateContextOnCompletion { get; set; } = true;

        public override bool ExecuteTask() {
            if (ArtifactDefinitions != null) {
                var artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, SolutionRoot, RelativeFrom);

                var commands = new VsoBuildCommands(Logger);
                var artifactService = new ArtifactService(Logger, new PhysicalFileSystem());
                artifactService.VsoCommands = commands;

                artifactService.FileVersion = FileVersion;
                artifactService.AssemblyVersion = AssemblyVersion;
                
                var storageInfo = artifactService.PublishArtifacts(Context, Path.GetFileName(SolutionRoot), artifacts);
            }

            return !Log.HasLoggedErrors;
        }
    }

    internal static class ArtifactPackageHelper {
        internal static List<ArtifactPackage> MaterializeArtifactPackages(ITaskItem[] artifactDefinitions, string solutionRoot, string[] relativeFrom) {
            List<ArtifactPackage> artifacts = new List<ArtifactPackage>();
            var grouping = artifactDefinitions.GroupBy(g => g.GetMetadata("ArtifactId"));

            foreach (var group in grouping) {
                List<PathSpec> pathSpecs = new List<PathSpec>();

                bool isAutomaticallyGenerated = false;

                if (solutionRoot != null) {
                    foreach (var file in group) {
                        string metadata = file.GetMetadata("Generated");
                        if (!string.IsNullOrWhiteSpace(metadata)) {
                            isAutomaticallyGenerated = bool.Parse(metadata);
                        }

                        var pathSpec = ArtifactPackage.CreatePathSpecification(
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

                var artifact = new ArtifactPackage(group.Key, pathSpecs) {
                    IsAutomaticallyGenerated = isAutomaticallyGenerated
                };

                artifacts.Add(artifact);
            }

            return artifacts;
        }
    }
}
