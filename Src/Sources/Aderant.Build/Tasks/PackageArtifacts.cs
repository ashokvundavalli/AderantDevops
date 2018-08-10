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

        public override bool Execute() {
            if (ArtifactDefinitions != null) {
                var artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, SolutionRoot, RelativeFrom);

                var commands = new VsoBuildCommands(Logger);
                var artifactService = new ArtifactService(new PhysicalFileSystem());
                artifactService.VsoCommands = commands;

                artifactService.FileVersion = FileVersion;
                artifactService.AssemblyVersion = AssemblyVersion;
                
                var storageInfo = artifactService.PublishArtifacts(Context, Path.GetFileName(SolutionRoot), artifacts);

                UpdateContext();
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

                if (solutionRoot != null) {
                    foreach (var file in group) {
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

                var artifact = new ArtifactPackage(group.Key, pathSpecs);
                artifacts.Add(artifact);
            }

            return artifacts;
        }
    }
}
