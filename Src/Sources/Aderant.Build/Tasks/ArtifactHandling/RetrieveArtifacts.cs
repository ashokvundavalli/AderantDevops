using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
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
            var service = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
            service.CommonOutputDirectory = CommonOutputDirectory;
            service.CommonDependencyDirectory = CommonDependencyDirectory;

            string containerKey = Path.GetFileName(SolutionRoot);

            if (Path.IsPathRooted(containerKey)) {
                throw new InvalidOperationException($"The container key {containerKey} cannot be a path.");
            }

            if (StagingDirectoryWhitelist != null) {
                var whitelist = StagingDirectoryWhitelist.Select(s => s.ItemSpec).ToArray();
                service.StagingDirectoryWhitelist = whitelist;

                Log.LogMessage("The following items are in the replication whitelist: " + string.Join(";", whitelist));
            }

            service.Resolve(Context, containerKey, SolutionRoot, WorkingDirectory);
            ArtifactRestoreSkipped = service.ArtifactRestoreSkipped;

            return !Log.HasLoggedErrors;
        }
    }

    internal class MSBuildEngine {
        private static MSBuildEngine instance;
        private BuildManager manager;
        private ProjectCollection projectCollection;

        public static MSBuildEngine DefaultEngine {
            get {
                if (instance == null) {
                    instance = new MSBuildEngine();
                }

                return instance;
            }
        }

        public BuildResult BuildProjectFile(string fullPath, string target, out bool targetExists, IDictionary<string, string> globalProperties = null) {
            if (manager == null) {
                manager = new BuildManager();
            }

            if (projectCollection == null) {
                projectCollection = new ProjectCollection();
                projectCollection.IsBuildEnabled = true;
            }

            if (globalProperties != null) {
                foreach (var prop in globalProperties) {
                    projectCollection.SetGlobalProperty(prop.Key, prop.Value);
                }
            }

            Project project = LoadProject(fullPath, projectCollection);
            ProjectInstance projectInstance = project.CreateProjectInstance();

            if (projectInstance.Targets.ContainsKey(target)) {
                var result = manager.Build(
                    new BuildParameters(projectCollection) { EnableNodeReuse = false, },
                    new BuildRequestData(
                        projectInstance,
                        new[] { target },
                        null,
                        BuildRequestDataFlags.ProvideProjectStateAfterBuild));

                if (globalProperties != null) {
                    foreach (var prop in globalProperties) {
                        projectCollection.RemoveGlobalProperty(prop.Key);
                    }
                }

                if (result.OverallResult == BuildResultCode.Failure) {
                    throw new Exception("Failed to evaluate: " + target, result.Exception);
                }

                targetExists = true;
                return result;
            }

            targetExists = false;
            return null;
        }

        protected virtual Project LoadProject(string directoryPropertiesFile, ProjectCollection collection) {
            return collection.LoadProject(directoryPropertiesFile);
        }
    }
}