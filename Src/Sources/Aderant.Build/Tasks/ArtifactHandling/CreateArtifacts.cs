using System.IO;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.Handlers;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.ArtifactHandling {
    public sealed class CreateArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        public string[] RelativeFrom { get; set; }

        public ITaskItem[] ArtifactDefinitions { get; set; }

        public string FileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public override bool ExecuteTask() {
            if (ArtifactDefinitions != null) {
                var artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, RelativeFrom, false);

                var artifactService = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
                artifactService.RegisterHandler(new PullRequestHandler());

                if (!Context.IsDesktopBuild) {
                    //artifactService.RegisterHandler(new XamlDropHandler(FileVersion, AssemblyVersion));
                }

                artifactService.CreateArtifacts(Context, Path.GetFileName(SolutionRoot), artifacts);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
