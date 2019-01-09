using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GenerateBuildPlan : BuildOperationContextTask {

        /// <summary>
        /// Gets or sets the build plan project file.
        /// That is the file that represents the tasks to perform in this build.
        /// </summary>
        [Required]
        public string BuildPlan { get; set; }

        [Required]
        public string[] ProjectFiles { get; set; }

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

        /// <summary>
        /// Gets or sets the make files.
        /// This eventually becomes additional directories to traverse into for cases when projects do not exist in the directory
        /// yet there are build targets that need to be executed.
        /// </summary>
        /// <value>The make files.</value>
        public string[] MakeFiles { get; set; }

        /// <summary>
        /// Gets or sets the extensibility files.
        /// These files contain instructions to influence the build.
        /// </summary>
        public string[] ExtensibilityFiles { get; set; }


        [Output]
        public string[] DirectoriesInBuild { get; set; }

        [Output]
        public string[] ImpactedTestAssemblies { get; set; }

        public override bool ExecuteTask() {
            ExecuteCore(Context);
            return !Log.HasLoggedErrors;
        }

        private void ExecuteCore(BuildOperationContext context) {

            if (context.Switches.Resume) {
                if (File.Exists(BuildPlan)) {
                    // When resuming we need to reconstruct the TrackedProjects collection.
                    // This is typically done in the sequencer but since we aren't entering that code path
                    // we need to load the existing set of known projects from our previous plan
                    RebuildFromExistingPlan(BuildPlan);
                    return;
                }
            }

            ExtensibilityController controller = new ExtensibilityController();
            var extensibilityImposition = controller.GetExtensibilityImposition(ExtensibilityFiles);

            var projectTree = ProjectTree.CreateDefaultImplementation(new BuildTaskLogger(Log));

            var jobFiles = new OrchestrationFiles {
                BeforeProjectFile = BeforeProjectFile,
                AfterProjectFile = AfterProjectFile,
                GroupExecutionFile = GroupExecutionFile,
                CommonProjectFile = CommonProjectFile,
                ExtensibilityImposition = extensibilityImposition,
                MakeFiles = MakeFiles,
                BuildPlan = BuildPlan,
            };

            context.ConfigurationToBuild = new ConfigurationToBuild(ConfigurationToBuild);

            var analysisContext = new AnalysisContext();
            analysisContext.ProjectFiles = ProjectFiles;

            var buildPlan = projectTree.ComputeBuildPlan(context, analysisContext, PipelineService, jobFiles).Result;
            if (buildPlan.DirectoriesInBuild != null) {
                DirectoriesInBuild = buildPlan.DirectoriesInBuild.ToArray();
            }

            var element = buildPlan.CreateXml();

            WritePlanToFile(context, element);

            ImpactedTestAssemblies = projectTree.LoadedConfiguredProjects.Where(proj => proj.AreTestsImpacted).Select(proj => proj.GetOutputAssemblyWithExtension()).ToArray();

            PipelineService.Publish(context);
        }

        private void WritePlanToFile(BuildOperationContext context, XElement element) {

            var settings = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                NewLineOnAttributes = true,
                IndentChars = "  ",
                Indent = true
            };

            using (var writer = XmlWriter.Create(Path.Combine(context.BuildRoot, BuildPlan), settings)) {
                element.WriteTo(writer);
            }
        }

        private void RebuildFromExistingPlan(string planFile) {
            var planLoader = new ExistingPlanLoader();
            DirectoriesInBuild = planLoader.LoadPlan(planFile, PipelineService).ToArray();
        }
    }

}