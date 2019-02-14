using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public ITaskItem[] ExtensibilityFiles { get; set; }

        public override bool ExecuteTask() {
            var service = new ArtifactService(PipelineService, new PhysicalFileSystem(), Logger);
            service.CommonOutputDirectory = CommonOutputDirectory;
            service.CommonDependencyDirectory = CommonDependencyDirectory;

            string containerKey = Path.GetFileName(SolutionRoot);

            if (Path.IsPathRooted(containerKey)) {
                throw new InvalidOperationException($"The container key {containerKey} cannot be a path.");
            }

            service.StagingDirectoryWhitelist = GetStagingDirectoryWhitelist();
            service.Resolve(Context, containerKey, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors;
        }

        private List<string> GetStagingDirectoryWhitelist() {
            List<string> whitelist = new List<string>();

            if (ExtensibilityFiles != null) {
                foreach (var file in ExtensibilityFiles) {
                    string fullPath = file.GetMetadata("FullPath");

                    if (File.Exists(fullPath)) {
                        string target = "GetStagingDirectoryWhitelist";

                        bool targetExists;
                        var results = MSBuildEngine.DefaultEngine.BuildProjectFile(fullPath, target, out targetExists);

                        if (targetExists && results != null && results.HasResultsForTarget(target)) {
                            ITaskItem[] taskItems = results.ResultsByTarget[target].Items;

                            foreach (var item in taskItems) {
                                whitelist.Add(ProjectCollection.Unescape(item.ItemSpec));
                            }
                        }
                    }
                }
            }

            return whitelist;
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