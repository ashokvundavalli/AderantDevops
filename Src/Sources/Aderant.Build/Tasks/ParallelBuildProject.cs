using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.Providers;
using Aderant.Build.VersionControl;
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
        public string JobFile { get; set; }

        /// <summary>
        /// Gets or sets the run file.
        /// That is the file that represents the coordination tasks for a build instance.
        /// </summary>
        [Required]
        public string JobRunFile { get; set; }

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

        [Output]
        public string[] ModulesInThisBuild { get; set; }

        protected override bool ExecuteTask(Context context) {
            ExecuteCore(context);
            return !Log.HasLoggedErrors;
        }

        private void ExecuteCore(Context context) {
            // TODO: keep this shim?
            context.BuildRoot = new DirectoryInfo(ModulesDirectory);

            var relationshipProcessing = GetRelationshipProcessingMode(context);
            var buildType = GetBuildType(context);

            IProjectTree projectTree = ProjectTree.CreateDefaultImplementation();
       
            var jobFiles = new BuildJobFiles {
                BeforeProjectFile = BeforeProjectFile,
                AfterProjectFile = AfterProjectFile,
                JobRunFile = JobRunFile,
                JobFile = JobFile,
            };

            Project project = projectTree.GenerateBuildJob(context, jobFiles).Result;
            var element = project.CreateXml();

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            settings.CloseOutput = true;
            settings.NewLineOnAttributes = true;
            settings.IndentChars = "    ";
            settings.Indent = true;

            using (var writer = XmlWriter.Create(Path.Combine(ModulesDirectory, JobFile), settings)) {
                element.WriteTo(writer);
            }

            //try {
            //    PhysicalFileSystem fileSystem = new PhysicalFileSystem(ModulesDirectory);

            //    BuildSequencer controller = new BuildSequencer(
            //        new BuildTaskLogger(this),
            //        context,
            //        new SolutionFileParser(),
            //        fileSystem,
            //        context.GetService<IVersionControlService>());

            //    IModuleProvider manifest = null;
            //    if (!string.IsNullOrEmpty(ProductManifest)) {
            //        manifest = ExpertManifest.Load(ProductManifest);
            //        ((ExpertManifest)manifest).ModulesDirectory = ModulesDirectory;
            //    } else {
            //        manifest = new ExpertManifest(fileSystem, new GlobalContext(TfvcBranch, TfvcChangeset));
            //    }

            //    IEnumerable<string> modulesInBuild;
            //    if (ModulesInBuild != null) {
            //        modulesInBuild = ModulesInBuild.Select(m => Path.GetFileName(m.ItemSpec));
            //    } else {
            //        modulesInBuild = manifest.GetAll().Select(s => s.Name);
            //    }

            //    modulesInBuild = modulesInBuild.Except(ExcludedModules ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);



            //    Project project = controller.CreateProject(ModulesDirectory, instance, BuildFrom, buildType, relationshipProcessing, context.Flavor);
            //    XElement projectDocument = controller.CreateProjectDocument(project);

            //    BuildSequencer.SaveBuildProject(Path.Combine(ModulesDirectory, JobFile), projectDocument);

            //    modulesInBuild = AddAliases(manifest, modulesInBuild);

            //    ModulesInThisBuild = Filter(modulesInBuild).ToArray();
            //} catch (Exception ex) {
            //    Log.LogErrorFromException(ex, true, true, sourceFilePath);
            //    throw;
            //}
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

        private static IEnumerable<string> Filter(IEnumerable<string> modulesInBuild) {
            // We treat _ as a special build system folder prefix
            foreach (var name in modulesInBuild) {
                if (name.StartsWith("_")) {
                    continue;
                }

                if (name.StartsWith(".")) {
                    continue;
                }

                yield return name;
            }
        }

        private static IEnumerable<string> AddAliases(IModuleProvider manifest, IEnumerable<string> modulesInBuild) {
            IModuleGroupingSupport groupingSupport = manifest as IModuleGroupingSupport;
            if (groupingSupport != null) {
                List<string> groupContainers = new List<string>();
                foreach (string name in modulesInBuild) {
                    ExpertModule container;
                    if (groupingSupport.TryGetContainer(name, out container)) {
                        groupContainers.Add(container.Name);
                    }
                }

                modulesInBuild = modulesInBuild.Union(groupContainers);
            }

            return modulesInBuild;
        }
    }

}
