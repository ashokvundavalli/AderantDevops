using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Packaging;
using Aderant.Build.TeamFoundation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public sealed class PublishArtifacts : ContextTaskBase {

        public string SolutionRoot { get; set; }

        public string[] RelativeFrom { get; set; }

        public ITaskItem[] Artifacts { get; set; }

        public string FileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public override bool Execute() {
            if (Artifacts != null) {
                var artifacts = MaterializeArtifactPackages();
                var artifactService = new ArtifactService(new PhysicalFileSystem(), new BucketService());

                artifactService.FileVersion = FileVersion;
                artifactService.AssemblyVersion = AssemblyVersion;

                var storageInfo = artifactService.PublishArtifacts(
                    Context,
                    SolutionRoot,
                    artifacts);

                var commands = new VsoBuildCommands(Logger);
                
                foreach (var item in storageInfo) {
                    commands.LinkArtifact(item.Name, VsoBuildArtifactType.FilePath, item.ComputeVsoPath());
                }
            }

            return !Log.HasLoggedErrors;
        }

        private List<ArtifactPackage> MaterializeArtifactPackages() {
            List<ArtifactPackage> artifacts = new List<ArtifactPackage>();
            var grouping = Artifacts.GroupBy(g => g.GetMetadata("ArtifactId"));

            foreach (var group in grouping) {
                List<PathSpec> pathSpecs = new List<PathSpec>();
                foreach (var file in group) {
                    var pathSpec = ArtifactPackage.CreatePathSpecification(
                        SolutionRoot,
                        RelativeFrom,
                        file.GetMetadata("FullPath"),
                        file.GetMetadata("TargetPath") // The destination location (assumed to be relative to "RelativeFrom")
                    );

                    if (!pathSpecs.Contains(pathSpec)) {
                        pathSpecs.Add(pathSpec);
                    }
                }

                var artifact = new ArtifactPackage(group.Key, pathSpecs);
                artifacts.Add(artifact);
            }

            return artifacts;
        }
    }

}
