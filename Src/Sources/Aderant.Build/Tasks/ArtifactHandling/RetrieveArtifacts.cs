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

        /// <summary>
        /// Gets or sets the optional common output directory.
        /// </summary>
        public string CommonOutputDirectory { get; set; }

        public override bool ExecuteTask() {
            var service = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
            service.CommonOutputDirectory = CommonOutputDirectory;

            string containerKey = Path.GetFileName(SolutionRoot);

            if (Path.IsPathRooted(containerKey)) {
                throw new InvalidOperationException($"The container key {containerKey} cannot be a path.");
            }

            service.Resolve(Context, containerKey, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors;
        }
    }

}
