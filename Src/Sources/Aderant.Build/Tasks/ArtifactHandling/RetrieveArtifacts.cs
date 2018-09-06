using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.ArtifactHandling {

    public sealed class RetrieveArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        public string Container { get; set; }

        public string WorkingDirectory { get; set; }

        public override bool ExecuteTask() {
            var service = new ArtifactService(Logger);
            service.Resolve(Context, Container, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors; 
        }
    }

}
