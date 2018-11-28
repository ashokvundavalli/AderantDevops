using System;
using System.IO;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.ArtifactHandling {

    public sealed class RetrieveArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        /// <summary>
        /// Gets or sets the working directory. The scratch directory where compressed files can be dumped etc.
        /// </summary>
        public string WorkingDirectory { get; set; }

        public override bool ExecuteTask() {
            var service = new ArtifactService(Logger);

            string containerKey = Path.GetFileName(SolutionRoot);

            if (Path.IsPathRooted(containerKey)) {
                throw new InvalidOperationException($"The container key {containerKey} cannot be a a rooted path");
            }

            service.Resolve(Context, containerKey, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors;
        }
    }

}
