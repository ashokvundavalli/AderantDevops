using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class ParallelBuildProjectFactory : BuildOperationContextTask {
        public ITaskItem[] ModulesInBuild { get; set; }

        public string TfvcChangeset { get; set; }

        public string TfvcBranch { get; set; }

        public string BuildFrom { get; set; }

        [Required]
        public string ModulesDirectory { get; set; }

        public string ProductManifest { get; set; }

        /// <summary>
        /// Gets or sets the instance project file.
        /// That is the file that represents the tasks to perform in this build.
        /// </summary>
        [Required]
        public string InstanceFile { get; set; }

        /// <summary>
        /// Gets or sets the targets file which performs the build orchestration
        /// That is the file that represents the coordination tasks for a build instance.
        /// </summary>
        [Required]
        public string GroupExecutionFile { get; set; }

        /// <summary>
        /// Gets or sets the before project file.
        /// That is the file that specifies prologue tasks to execute for each solution.
        /// </summary>
        [Required]
        public string BeforeProjectFile { get; set; }

        /// <summary>
        /// Gets or sets the after project file.
        /// That is the file that specifies epilogue tasks to execute for each solution.
        /// </summary>
        [Required]
        public string AfterProjectFile { get; set; }

        /// <summary>
        /// Gets or sets the project which can define properties to inject into each build group execution.
        /// </summary>
        public string CommonProjectFile { get; set; }

        public string ConfigurationToBuild { get; set; }

        [Output]
        public string[] ModulesInThisBuild { get; set; }

        public string[] ExcludePaths { get; set; } = new string[0];

        protected override bool UpdateContextOnCompletion { get; set; } = true;

        public override bool ExecuteTask() {
            ExecuteCore(Context);
            return !Log.HasLoggedErrors;
        }

        private void ExecuteCore(BuildOperationContext context) {
            // TODO: keep this shim?
            context.BuildRoot = new DirectoryInfo(ModulesDirectory);

            if (context.Switches.Resume) {
                if (File.Exists(InstanceFile)) {
                    return;
                }
            }

            var projectTree = ProjectTree.CreateDefaultImplementation(new BuildTaskLogger(Log));

            var jobFiles = new OrchestrationFiles {
                BeforeProjectFile = BeforeProjectFile,
                AfterProjectFile = AfterProjectFile,
                GroupExecutionFile = GroupExecutionFile,
                CommonProjectFile = CommonProjectFile,
                InstanceFile = InstanceFile,
            };

            var analysisContext = CreateAnalysisContext();
            context.ConfigurationToBuild = new ConfigurationToBuild(ConfigurationToBuild);

            var project = projectTree.ComputeBuildSequence(context, analysisContext, jobFiles).Result;
            var element = project.CreateXml();

            var settings = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                NewLineOnAttributes = true,
                IndentChars = "  ",
                Indent = true
            };

            using (var writer = XmlWriter.Create(Path.Combine(ModulesDirectory, InstanceFile), settings)) {
                element.WriteTo(writer);
            }
        }

        private AnalysisContext CreateAnalysisContext() {
            ErrorUtilities.IsNotNull(Context.BuildSystemDirectory, nameof(Context.BuildSystemDirectory));

            var paths = ExcludePaths.ToList();

            if (!Context.BuildSystemDirectory.Contains("TestResults")) {
                paths.Add(Context.BuildSystemDirectory);
            }

            paths.Add(".git");
            paths.Add("$");

            var analysisContext = new AnalysisContext {
                ExcludePaths = paths
                    .Select(PathUtility.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            Log.LogMessage("Excluding paths: " + string.Join("|", paths));

            return analysisContext;
        }
    }

    internal class AnalysisContext {
        public IReadOnlyCollection<string> ExcludePaths { get; set; }
    }

}
