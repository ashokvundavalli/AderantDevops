using System;
using System.IO;
using System.Linq;
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
        /// <remarks>
        /// Example usage is to replicate artifacts to a common dependency directory during a build
        /// to ensure downstream projects have access the outputs of their predecessors
        /// </remarks>
        public string CommonDependencyDirectory { get; set; }

        public ITaskItem[] StagingDirectoryWhitelist { get; set; }

        [Output]
        public bool ArtifactRestoreSkipped { get; set; }

        public override bool ExecuteTask() {
            var service = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger) {
                CommonOutputDirectory = CommonOutputDirectory,
                CommonDependencyDirectory = CommonDependencyDirectory
            };

            string containerKey = Path.GetFileName(SolutionRoot);

            if (Path.IsPathRooted(containerKey)) {
                throw new InvalidOperationException($"The container key {containerKey} cannot be a path.");
            }

            if (StagingDirectoryWhitelist != null) {
                var whitelist = StagingDirectoryWhitelist.Select(s => s.ItemSpec).ToArray();
                service.StagingDirectoryWhitelist = whitelist;

                Log.LogMessage("The following items are in the replication whitelist: " + string.Join(";", whitelist), null);
            }

            service.Resolve(containerKey, SolutionRoot, WorkingDirectory);
            ArtifactRestoreSkipped = service.ArtifactRestoreSkipped;

            return !Log.HasLoggedErrors;
        }
    }
}
