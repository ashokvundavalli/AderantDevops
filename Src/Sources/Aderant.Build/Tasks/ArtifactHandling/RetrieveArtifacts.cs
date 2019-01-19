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
        /// This is the usually 'bin\module' by convention.
        /// </summary>
        public string CommonOutputDirectory { get; set; }

        /// <summary>
        /// Additional destination directories for the artifacts.
        /// </summary>
        /// <remarks>Example usage is to replicate artifacts to a shared dependency directory during a build
        /// to ensure downstream projects have access the outputs of their predecessors</remarks>
        public string[] DestinationDirectories { get; set; }

        public override bool ExecuteTask() {
            var service = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
            service.CommonOutputDirectory = CommonOutputDirectory;
            service.DestinationDirectories = DestinationDirectories;

            string containerKey = Path.GetFileName(SolutionRoot);

            if (Path.IsPathRooted(containerKey)) {
                throw new InvalidOperationException($"The container key {containerKey} cannot be a path.");
            }

            service.Resolve(Context, containerKey, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors;
        }
    }

}
