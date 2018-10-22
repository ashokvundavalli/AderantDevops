using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;
using Project = Aderant.Build.MSBuild.Project;

namespace Aderant.Build.Tasks {
    public sealed class GenerateBuildPlan : BuildOperationContextTask {
        public ITaskItem[] ModulesInBuild { get; set; }

        public string TfvcChangeset { get; set; }

        public string TfvcBranch { get; set; }

        public string BuildFrom { get; set; }

        [Required]
        public string ModulesDirectory { get; set; }

        public string ProductManifest { get; set; }

        /// <summary>
        /// Gets or sets the build plan project file.
        /// That is the file that represents the tasks to perform in this build.
        /// </summary>
        [Required]
        public string BuildPlan { get; set; }

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

        /// <summary>
        /// Gets or sets the extensibility files.
        /// These files contain instructions to influence the build.
        /// </summary>
        public string[] ExtensibilityFiles { get; set; }

        public override bool ExecuteTask() {
            ExecuteCore(Context);
            return !Log.HasLoggedErrors;
        }

        private void ExecuteCore(BuildOperationContext context) {
            // TODO: keep this shim?
            context.BuildRoot = ModulesDirectory;

            if (context.Switches.Resume) {
                if (File.Exists(BuildPlan)) {
                    return;
                }
            }

            ExtensibilityImposition extensibilityImposition = null;
            if (ExtensibilityFiles != null) {
                extensibilityImposition = ExtensibilityController.GetExtensibilityImposition(ModulesDirectory, ExtensibilityFiles);
            }

            var projectTree = ProjectTree.CreateDefaultImplementation(new BuildTaskLogger(Log));

            var jobFiles = new OrchestrationFiles {
                BeforeProjectFile = BeforeProjectFile,
                AfterProjectFile = AfterProjectFile,
                GroupExecutionFile = GroupExecutionFile,
                CommonProjectFile = CommonProjectFile,
                BuildPlan = BuildPlan,
            };

            var analysisContext = CreateAnalysisContext();
            analysisContext.ExtensibilityImposition = extensibilityImposition;
            context.ConfigurationToBuild = new ConfigurationToBuild(ConfigurationToBuild);

            Project buildPlan = projectTree.ComputeBuildPlan(context, analysisContext, PipelineService, jobFiles).Result;

            var element = buildPlan.CreateXml();

            ModulesInThisBuild = buildPlan.ModuleNames.ToArray();

            var settings = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                NewLineOnAttributes = true,
                IndentChars = "  ",
                Indent = true
            };

            using (var writer = XmlWriter.Create(Path.Combine(ModulesDirectory, BuildPlan), settings)) {
                element.WriteTo(writer);
            }

            PipelineService.Publish(context);
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

            Log.LogMessage("Excluding paths: " + string.Join("|", analysisContext.ExcludePaths));

            return analysisContext;
        }
    }
}
