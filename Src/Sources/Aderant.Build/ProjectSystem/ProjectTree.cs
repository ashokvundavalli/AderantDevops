using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.Services;
using Aderant.Build.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {

    /// <summary>
    /// A <see cref="ProjectTree" /> models the relationship between projects in a solution, and cross solution project
    /// references and their external dependencies.
    /// </summary>
    [Export(typeof(IProjectTree))]
    internal class ProjectTree : IProjectTree, IProjectTreeInternal, ISolutionManager {

        // Holds all projects that are applicable to the build tree
        private readonly ConcurrentDictionary<Guid, ConfiguredProject> loadedConfiguredProjects = new ConcurrentDictionary<Guid, ConfiguredProject>();
        private readonly ILogger logger = NullLogger.Default;

        private ConcurrentBag<UnconfiguredProject> loadedUnconfiguredProjects;

        // Holds any projects which we cannot load a solution for
        private ConcurrentBag<ConfiguredProject> orphanedProjects = new ConcurrentBag<ConfiguredProject>();

        // First level cache for parsed solution information
        // Also stores the data needed to create the "CurrentSolutionConfigurationContents" object that
        // MSBuild uses when building from a solution. Unfortunately the build engine doesn't let us build just projects
        // due to the way that AssignProjectConfiguration in Microsoft.Common.CurrentVersion.targets assumes that you are coming from a solution
        // and attempts to assign platforms and targets to project references, even if you are not building them.
        private Dictionary<Guid, ProjectInSolution> projectsByGuid = new Dictionary<Guid, ProjectInSolution>();
        private Dictionary<Guid, string> projectToSolutionMap = new Dictionary<Guid, string>();

        public ProjectTree() {

        }

        [ImportingConstructor]
        public ProjectTree(ILogger logger) {
            this.logger = logger;
            SolutionManager = this;
        }

        internal ProjectTree(IEnumerable<UnconfiguredProject> unconfiguredProjects)
            : this((ILogger)null) {

            EnsureUnconfiguredProjects();

            foreach (var project in unconfiguredProjects) {
                loadedUnconfiguredProjects.Add(project);
            }
        }

        [Import]
        private ExportFactory<UnconfiguredProject> UnconfiguredProjectFactory { get; set; }

        [Import(AllowDefault = true)]
        public ExportFactory<ISequencer> SequencerFactory { get; set; }

        public IReadOnlyCollection<UnconfiguredProject> LoadedUnconfiguredProjects {
            get { return loadedUnconfiguredProjects; }
        }

        public IReadOnlyCollection<ConfiguredProject> LoadedConfiguredProjects {
            get { return loadedConfiguredProjects.Values.ToList(); }
        }

        [Import]
        public IProjectServices Services { get; internal set; }

        public ISolutionManager SolutionManager { get; set; }

        public void LoadProjects(string directory, bool recursive, IReadOnlyCollection<string> excludeFilterPatterns) {
            LoadProjects(new[] { directory }, recursive, excludeFilterPatterns);
        }

        public void LoadProjects(IReadOnlyCollection<string> directories, bool recursive, IReadOnlyCollection<string> excludeFilterPatterns) {
            EnsureUnconfiguredProjects();

            ConcurrentDictionary<string, byte> files = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            logger.Info("Raw scanning paths: " + string.Join(",", directories));

            if (excludeFilterPatterns != null) {
                excludeFilterPatterns = excludeFilterPatterns.Select(PathUtility.GetFullPath).ToList();

                logger.Info("Excluding paths: " + string.Join(",", excludeFilterPatterns));
            }

            Parallel.ForEach(
                directories,
                new ParallelOptions {
                    MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism
                },
                (path) => {
                    foreach (var file in GrovelForFiles(path, excludeFilterPatterns)) {
                        files.TryAdd(file, 0);
                    }
                });


            projectCollection = new ProjectCollection();
            projectCollection.SkipEvaluation = true;

            ActionBlock<string> parseBlock = new ActionBlock<string>(s => LoadAndParseProjectFile(s), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism });

            foreach (var file in files.Keys) {
                parseBlock.Post(file);
            }

            parseBlock.Complete();
            parseBlock.Completion.GetAwaiter().GetResult();
        }

        public async Task CollectBuildDependencies(BuildDependenciesCollector collector) {
            // Null checked to allow unit testing where projects are inserted directly
            if (LoadedUnconfiguredProjects != null) {
                ErrorUtilities.IsNotNull(collector.ProjectConfiguration, nameof(collector.ProjectConfiguration));

                foreach (var unconfiguredProject in LoadedUnconfiguredProjects) {
                    try {
                        if (!unconfiguredProject.IsTemplateProject()) {
                            ConfiguredProject project = unconfiguredProject.LoadConfiguredProject(this);
                            project.AssignProjectConfiguration(collector.ProjectConfiguration);
                        } else {
                            if (logger != null) {
                                logger.Info("Ignored template project: {0}", unconfiguredProject.FullPath);
                            }
                        }
                    } catch (Exception ex) {
                        if (logger != null) {
                            logger.Error("Project {0} failed to load. {1}", unconfiguredProject.FullPath, ex.Message);
                        }

                        if (ex is DuplicateGuidException) {
                            throw;
                        }
                    }
                }
            }

            foreach (var project in LoadedConfiguredProjects) {
                try {
                    BuildCurrentSolutionConfigurationXml(collector.ProjectConfiguration);

                    await project.CollectBuildDependencies(collector);
                } catch (Exception ex) {
                    logger.LogErrorFromException(ex, false, false);
                    throw;
                }
            }
        }

        private void BuildCurrentSolutionConfigurationXml(ConfigurationToBuild collectorProjectConfiguration) {
            var projectsInSolutions = projectsByGuid.Values;

            var generator = new SolutionConfigurationContentsGenerator(collectorProjectConfiguration);
            MetaprojectXml = generator.CreateSolutionProject(projectsInSolutions);
        }

        public string MetaprojectXml { get; private set; }

        public DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector) {
            foreach (var project in LoadedConfiguredProjects) {

                try {
                    project.AnalyzeBuildDependencies(collector);
                } catch (Exception ex) {
                    logger.LogErrorFromException(ex, false, false);
                    throw;
                }

                if (collector.SourceChanges != null) {
                    project.CalculateDirtyStateFromChanges(collector.SourceChanges);
                }
            }

            var graph = CreateBuildDependencyGraphInternal();
            return new DependencyGraph(graph);
        }

        public async Task<BuildPlan> ComputeBuildPlan(BuildOperationContext context, AnalysisContext analysisContext, IBuildPipelineService pipelineService, OrchestrationFiles jobFiles) {
            LoadProjects(analysisContext.ProjectFiles);

            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = context.ConfigurationToBuild;
            collector.ExtensibilityImposition = jobFiles.ExtensibilityImposition;

            await CollectBuildDependencies(collector);

            if (context.SourceTreeMetadata != null) {
                if (context.SourceTreeMetadata.Changes != null) {
                    // Feed the file changes in so we can calculate the dirty projects.
                    collector.SourceChanges = context.SourceTreeMetadata.Changes;
                }
            }

            DependencyGraph graph = CreateBuildDependencyGraph(collector);

            using (var exportLifetimeContext = SequencerFactory.CreateExport()) {
                var sequencer = exportLifetimeContext.Value;
                sequencer.PipelineService = pipelineService;
                sequencer.MetaprojectXml = this.MetaprojectXml;

                BuildPlan plan = sequencer.CreatePlan(context, jobFiles, graph);
                return plan;
            }
        }

        private void LoadProjects(IReadOnlyCollection<string> analysisContextProjectFiles) {
            EnsureUnconfiguredProjects();

            foreach (string projectFile in analysisContextProjectFiles) {
                LoadAndParseProjectFile(projectFile);
            }
        }

        public void AddConfiguredProject(ConfiguredProject configuredProject) {
            if (configuredProject.IncludeInBuild) {
                ConfiguredProject existing;
                if (loadedConfiguredProjects.TryGetValue(configuredProject.ProjectGuid, out existing)) {
                    throw new DuplicateGuidException(configuredProject.ProjectGuid, $"The project GUID {configuredProject.ProjectGuid} in file {configuredProject.FullPath} has already been assigned to {existing.FullPath}. Duplicate GUIDs are not supported.");
                }

                loadedConfiguredProjects.TryAdd(configuredProject.ProjectGuid, configuredProject);
            }
        }

        public void OrphanProject(ConfiguredProject configuredProject) {
            configuredProject.IncludeInBuild = false;
            logger.Warning($"Orphaned project: '{configuredProject.FullPath}' (not referenced by any solution).");
            this.orphanedProjects.Add(configuredProject);
        }

        public SolutionSearchResult GetSolutionForProject(string projectFilePath, Guid projectGuid) {
            ProjectInSolution projectInSolution;
            projectsByGuid.TryGetValue(projectGuid, out projectInSolution);

            string solutionFile;
            projectToSolutionMap.TryGetValue(projectGuid, out solutionFile);

            if (projectInSolution == null || solutionFile == null) {

                FileInfo fileInfo = new FileInfo(projectFilePath);
                DirectoryInfo directoryToSearch = fileInfo.Directory;

                do {
                    if (directoryToSearch != null) {
                        var files = Services.FileSystem.GetFiles(directoryToSearch.FullName, "*.sln", false);

                        var exactMatch = files.FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), directoryToSearch.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (exactMatch != null) {
                            files = new[] { exactMatch };
                        }

                        foreach (var file in files) {
                            if (file.EndsWith(".Custom.sln", StringComparison.InvariantCultureIgnoreCase)) {
                                continue;
                            }

                            var parser = InitializeSolutionParser();
                            ParseResult parseResult = parser.Parse(file);

                            foreach (var p in parseResult.ProjectsByGuid) {
                                if (!projectToSolutionMap.ContainsKey(p.Key)) {
                                    projectToSolutionMap.Add(p.Key, parseResult.SolutionFile);
                                }

                                if (!projectsByGuid.ContainsKey(p.Key)) {
                                    projectsByGuid.Add(p.Key, p.Value);
                                }
                            }

                            if (parseResult.ProjectsByGuid.TryGetValue(projectGuid, out projectInSolution)) {
                                if (exactMatch != null) {
                                    logger.Info($"Found exact solution match for project {projectFilePath} -> {exactMatch}");
                                } else {
                                    logger.Info($"Found inexact solution match for project {projectFilePath} -> {file}");
                                }

                                solutionFile = parseResult.SolutionFile;
                                directoryToSearch = null;
                                break;
                            }
                        }

                        if (directoryToSearch != null) {
                            directoryToSearch = directoryToSearch.Parent;
                        }
                    }
                } while (directoryToSearch != null);
            }

            if (solutionFile == null) {
                return new SolutionSearchResult(null, null) { Found = false };
            }

            return new SolutionSearchResult(solutionFile, projectInSolution);
        }

        private void EnsureUnconfiguredProjects() {
            if (loadedUnconfiguredProjects == null) {
                loadedUnconfiguredProjects = new ConcurrentBag<UnconfiguredProject>();
            }
        }

        internal IEnumerable<string> GrovelForFiles(string directory, IReadOnlyCollection<string> excludeFilterPatterns) {
            var filePathCollector = new List<string>();

            GetFilesWithExtensionRecursive(filePathCollector, directory);

            return DirectoryGroveler.FilterFiles(filePathCollector, excludeFilterPatterns);
        }

        string[] extensions = new[] {
            "*.csproj",
            "*.wixproj",
        };

        string[] directoryFilter = new[] {
            "packages",
            "dependencies",
        };

        private ProjectCollection projectCollection;

        private void GetFilesWithExtensionRecursive(List<string> filePathCollector, string directory) {
            // For performance reasons it is important to avoid known symlink directories so here we do not traverse into them
            IEnumerable<string> directories = Services.FileSystem.GetDirectories(directory, false);

            foreach (var dir in directories) {
                bool process = true;

                bool doExtensionFilter = Services.FileSystem.GetFiles(dir, "*.sln", false).Any();
                if (doExtensionFilter) {
                    foreach (var dirFilter in directoryFilter) {
                        if (dir.IndexOf(dirFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                            process = false;
                            break;
                        }
                    }
                }

                if (process) {
                    foreach (var extension in extensions) {
                        filePathCollector.AddRange(Services.FileSystem.GetFiles(directory, extension, false));
                    }

                    GetFilesWithExtensionRecursive(filePathCollector, dir);
                }
            }
        }

        private IEnumerable<IArtifact> CreateBuildDependencyGraphInternal() {
            List<IArtifact> graph = new List<IArtifact>();

            var comparer = DependencyEqualityComparer.Default;

            // This HashSet is not used for anything?
            HashSet<IDependable> allDependencies = new HashSet<IDependable>(comparer);

            ProcessProjects(allDependencies, graph);

            return graph;
        }

        private void ProcessProjects(HashSet<IDependable> allDependencies, List<IArtifact> graph) {
            foreach (var project in LoadedConfiguredProjects) {

                IReadOnlyCollection<IDependable> dependables = project.GetDependencies();

                foreach (var dependency in dependables) {
                    allDependencies.Add(dependency);
                }

                graph.Add(project);
            }
        }

        private SolutionFileParser InitializeSolutionParser() {
            var parser = new SolutionFileParser();
            return parser;
        }

        private void LoadAndParseProjectFile(string file) {
            using (Stream stream = Services.FileSystem.OpenFile(file)) {
                using (var reader = XmlReader.Create(stream)) {

                    UnconfiguredProject unconfiguredProject;

                    using (var exportLifetimeContext = UnconfiguredProjectFactory.CreateExport()) {
                        unconfiguredProject = exportLifetimeContext.Value;

                        unconfiguredProject.ProjectCollection = projectCollection;
                        unconfiguredProject.Initialize(reader, file);

                        loadedUnconfiguredProjects.Add(unconfiguredProject);
                    }
                }
            }
        }

        public static IProjectTree CreateDefaultImplementation(ILogger logger, IReadOnlyCollection<Assembly> assemblies, bool throwCompositionErrors = false) {
            return GetProjectService(CreateSelfHostContainer(logger, assemblies, throwCompositionErrors));
        }

        private static IProjectTree GetProjectService(ServiceContainer createSelfHostContainer) {
            return createSelfHostContainer.GetExportedValue<IProjectTree>();
        }

        private static ServiceContainer CreateSelfHostContainer(ILogger logger, IReadOnlyCollection<Assembly> assemblies, bool throwCompositionErrors) {
            return new ServiceContainer(logger, assemblies);
        }

        /// <summary>
        /// Creates the default project tree implementation.
        /// </summary>
        public static IProjectTree CreateDefaultImplementation(ILogger logger) {
            return CreateDefaultImplementation(logger, new[] { typeof(ProjectTree).Assembly });
        }
    }

    internal interface ISequencer {

        IBuildPipelineService PipelineService { get; set; }

        /// <summary>
        /// Gets or sets the solution meta configuration.
        /// This is the SolutionConfiguration data from the sln.metaproj
        /// </summary>
        string MetaprojectXml { get; set; }

        BuildPlan CreatePlan(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph);
    }
}
