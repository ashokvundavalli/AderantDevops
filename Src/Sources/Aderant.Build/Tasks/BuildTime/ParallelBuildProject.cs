using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.MSBuild;
using Aderant.Build.Providers;
using Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer;
using Aderant.Build.Tasks.BuildTime.Sequencer;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks.BuildTime {
    public sealed class ParallelBuildProjectFactory : ContextTaskBase {
        public ITaskItem[] ModulesInBuild { get; set; }

        public string[] ExcludedModules { get; set; }

        public string TfvcChangeset { get; set; }

        public string TfvcBranch { get; set; }

        public string BuildFrom { get; set; }

        [Required]
        public string ModulesDirectory { get; set; }

        public string ProductManifest { get; set; }

        [Required]
        public string ProjectFile { get; set; }

        public string[] CodeAnalysisGroup { get; set; }

        public bool IsComboBuild { get; set; }

        public string ComboBuildType { get; set; }

        public string DownStreamType { get; set; }

        public string ComboBuildProjectFile { get; set; }

        [Output]
        public string[] ModulesInThisBuild { get; set; }

        protected override bool ExecuteTask(Context context) {
            Run();
            return !Log.HasLoggedErrors;
        }

        private void Run([CallerFilePath] string sourceFilePath = "") {
            try {
                PhysicalFileSystem fileSystem = new PhysicalFileSystem(ModulesDirectory);

                BuildSequencer controller = new BuildSequencer(
                    new BuildTaskLogger(this),
                    new SolutionFileParser(),
                    new RepositoryInfoProvider(fileSystem, TfvcBranch, TfvcChangeset),
                    fileSystem);

                IModuleProvider manifest = null;
                if (!string.IsNullOrEmpty(ProductManifest)) {
                    manifest = ExpertManifest.Load(ProductManifest);
                    ((ExpertManifest)manifest).ModulesDirectory = ModulesDirectory;
                } else {
                    manifest = new ExpertManifest(fileSystem, new GlobalContext(TfvcBranch, TfvcChangeset));
                }

                IEnumerable<string> modulesInBuild;
                if (ModulesInBuild != null) {
                    modulesInBuild = ModulesInBuild.Select(m => Path.GetFileName(m.ItemSpec));
                } else {
                    modulesInBuild = manifest.GetAll().Select(s => s.Name);
                }

                modulesInBuild = modulesInBuild.Except(ExcludedModules ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

                //Log.LogMessage($"CodeAnalysisGroup: {string.Join(",", CodeAnalysisGroup)}. Contains: {CodeAnalysisGroup.Contains("Case")}");
                Log.LogMessage("Creating dynamic project...");

                ComboBuildType buildType = (ComboBuildType)Enum.Parse(typeof(ComboBuildType), ComboBuildType);
                DownStreamType downstreamType = (DownStreamType)Enum.Parse(typeof(DownStreamType), DownStreamType);
                Project project = controller.CreateProject(ModulesDirectory, manifest, modulesInBuild, BuildFrom, IsComboBuild, ComboBuildProjectFile, buildType, downstreamType);
                XElement projectDocument = controller.CreateProjectDocument(project);

                BuildSequencer.SaveBuildProject(Path.Combine(ModulesDirectory, ProjectFile), projectDocument);

                modulesInBuild = AddAliases(manifest, modulesInBuild);

                ModulesInThisBuild = Filter(modulesInBuild).ToArray();
            } catch (Exception ex) {
                Log.LogErrorFromException(ex, true, true, sourceFilePath);
                throw;
            }
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

    internal class RepositoryInfoProvider {
        private readonly IFileSystem2 fileSystem2;
        private readonly string tfvcBranch;
        private readonly string tfvcChangeSet;

        public RepositoryInfoProvider(IFileSystem2 fileSystem2, string tfvcBranch, string tfvcChangeSet) {
            this.fileSystem2 = fileSystem2;
            this.tfvcBranch = tfvcBranch;
            this.tfvcChangeSet = tfvcChangeSet;
        }

        public void GetRepositoryInfo(string path, out RepositoryType type, out string tfvcBranch, out string tfvcChangeSet) {
            var repoDirectory = Path.Combine(path, ".git");

            tfvcBranch = null;
            tfvcChangeSet = null;

            if (fileSystem2.DirectoryExists(repoDirectory)) {
                type = RepositoryType.Git;
            } else {
                tfvcBranch = this.tfvcBranch;
                tfvcChangeSet = this.tfvcChangeSet;
                type = RepositoryType.Tfvc;
            }
        }
    }

    public enum ComboBuildType {
        Changed,
        Branch,
        Staged,
        All
    }

    public enum DownStreamType {
        Direct,
        All,
        None
    }
}