using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class ParallelBuildProjectFactory : ContextTaskBase {
        public ITaskItem[] ModulesInBuild { get; set; }

        public string[] ExcludedModules { get; set; }

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
        public string GroupOrchestrationFile { get; set; }

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

        public string ConfigurationToBuild { get; set; }

        [Output]
        public string[] ModulesInThisBuild { get; set; }

        public string[] ExcludePaths { get; set; }

        public override bool Execute() {
            ExecuteCore(Context);
            return !Log.HasLoggedErrors;
        }

        private void ExecuteCore(Context context) {
            // TODO: keep this shim?
            context.BuildRoot = new DirectoryInfo(ModulesDirectory);

            var relationshipProcessing = GetRelationshipProcessingMode(context);
            var buildType = GetBuildType(context);

            if (context.Switches.Resume) {
                if (File.Exists(InstanceFile)) {
                    return;
                }
            }

            var projectTree = ProjectTree.CreateDefaultImplementation();

            var jobFiles = new BuildJobFiles {
                BeforeProjectFile = BeforeProjectFile,
                AfterProjectFile = AfterProjectFile,
                GroupOrchestrationFile = GroupOrchestrationFile,
                InstanceFile = InstanceFile,
            };

            var analysisContext = CreateAnalysisContext();
            context.ConfigurationToBuild = ConfigurationToBuild;
            
            var project = projectTree.GenerateBuildJob(context, analysisContext, jobFiles).Result;
            var element = project.CreateXml();

            var settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            settings.CloseOutput = true;
            settings.NewLineOnAttributes = true;
            settings.IndentChars = "    ";
            settings.Indent = true;

            using (var writer = XmlWriter.Create(Path.Combine(ModulesDirectory, InstanceFile), settings)) {
                element.WriteTo(writer);
            }
        }

        private AnalysisContext CreateAnalysisContext() {
            var paths = ExcludePaths.ToList();
            paths.Add(Context.BuildSystemDirectory);

            var analysisContext = new AnalysisContext {
                ExcludePaths = paths
            };
            return analysisContext;
        }

        private static ProjectRelationshipProcessing GetRelationshipProcessingMode(Context context) {
            ProjectRelationshipProcessing relationshipProcessing = ProjectRelationshipProcessing.None;
            if (context.Switches.Downstream) {
                relationshipProcessing = ProjectRelationshipProcessing.Direct;
            }

            if (context.Switches.Transitive) {
                relationshipProcessing = ProjectRelationshipProcessing.Transitive;
            }

            return relationshipProcessing;
        }

        private static ComboBuildType GetBuildType(Context context) {
            ComboBuildType buildType = ComboBuildType.All;
            if (context.Switches.PendingChanges) {
                buildType = ComboBuildType.Changes;
            }

            if (context.Switches.Everything) {
                buildType = ComboBuildType.Branch;
            }

            return buildType;
        }

    }

    internal class AnalysisContext  {
        public IReadOnlyCollection<string> ExcludePaths { get; set; }
    }

}
