using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.Services;
using Aderant.Build.Tasks;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {

    /// <summary>
    /// A <see cref="ProjectTree" /> models the relationship between projects in a solution, and cross solution project
    /// references and their external dependencies.
    /// </summary>
    [Export(typeof(IProjectTree))]
    internal class ProjectTree : IProjectTree, IProjectTreeInternal, ISolutionManager {

        // Holds all projects that are applicable to the build tree
        private readonly ConcurrentBag<ConfiguredProject> loadedConfiguredProjects = new ConcurrentBag<ConfiguredProject>();
        private readonly ILogger logger = NullLogger.Default;

        private ConcurrentBag<UnconfiguredProject> loadedUnconfiguredProjects;

        // Holds any projects which we cannot load a solution for
        private ConcurrentBag<ConfiguredProject> orphanedProjects = new ConcurrentBag<ConfiguredProject>();

        // First level cache for parsed solution information
        private Dictionary<Guid, ProjectInSolution> projectsByGuid = new Dictionary<Guid, ProjectInSolution>();
        private Dictionary<Guid, string> projectToSolutionMap = new Dictionary<Guid, string>();

        public ProjectTree() {

        }

        [ImportingConstructor]
        public ProjectTree(ILogger logger) {
            this.logger = logger;
        }

        [Import]
        private ExportFactory<UnconfiguredProject> UnconfiguredProjectFactory { get; set; }

        [Import(AllowDefault = true)]
        public ExportFactory<ISequencer> SequencerFactory { get; set; }

        public IReadOnlyCollection<UnconfiguredProject> LoadedUnconfiguredProjects {
            get { return loadedUnconfiguredProjects; }
        }

        public IReadOnlyCollection<ConfiguredProject> LoadedConfiguredProjects {
            get { return loadedConfiguredProjects; }
        }

        [Import]
        public IProjectServices Services { get; internal set; }

        public ISolutionManager SolutionManager {
            get { return this; }
        }

        public void LoadProjects(string directory, bool recursive, IReadOnlyCollection<string> excludeFilterPatterns) {
            LoadProjects(new[] { directory }, recursive, excludeFilterPatterns);
        }

        public void LoadProjects(IReadOnlyCollection<string> directory, bool recursive, IReadOnlyCollection<string> excludeFilterPatterns) {
            if (loadedUnconfiguredProjects == null) {
                loadedUnconfiguredProjects = new ConcurrentBag<UnconfiguredProject>();
            }

            ConcurrentDictionary<string, byte> files = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            Parallel.ForEach(
                directory,
                (path) => {
                    foreach (var file in GrovelForFiles(path, excludeFilterPatterns)) {
                        files.TryAdd(file, 0);
                    }
                });

            var defaultCopyParallelism = Environment.ProcessorCount > 4 ? 6 : 4;

            ActionBlock<string> parseBlock = new ActionBlock<string>(s => LoadAndParseProjectFile(s), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = defaultCopyParallelism });

            foreach (var file in files.Keys) {
                parseBlock.Post(file);
            }

            parseBlock.Complete();
            parseBlock.Completion.GetAwaiter().GetResult();
        }

        public async Task CollectBuildDependencies(BuildDependenciesCollector collector) {
            // Null checked to allow unit testing where projects are inserted directly
            if (LoadedUnconfiguredProjects != null) {
                foreach (var unconfiguredProject in LoadedUnconfiguredProjects) {
                    try {
                        ConfiguredProject project = unconfiguredProject.LoadConfiguredProject();
                        project.AssignProjectConfiguration(collector.ProjectConfiguration);
                    } catch (Exception ex) {
                        logger.Warning("Project {0} failed to load. {1}", unconfiguredProject.FullPath, ex.Message);
                    }
                }
            }

            foreach (var project in LoadedConfiguredProjects) {
                await project.CollectBuildDependencies(collector);
            }
        }

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

        public async Task<Project> ComputeBuildPlan(BuildOperationContext context, AnalysisContext analysisContext, IBuildPipelineService pipelineService, OrchestrationFiles jobFiles) {
            List<string> includePaths = new List<string> { context.BuildRoot };
            if (context.Include != null) {
                includePaths.AddRange(context.Include);
            }

            LoadProjects(includePaths, true, analysisContext.ExcludePaths);

            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = context.ConfigurationToBuild;

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

                Project project = sequencer.CreateProject(context, analysisContext, jobFiles, graph);
                return project;
            }
        }

        public void AddConfiguredProject(ConfiguredProject configuredProject) {
            if (configuredProject.IncludeInBuild) {
                loadedConfiguredProjects.Add(configuredProject);
            }
        }

        public void OrphanProject(ConfiguredProject configuredProject) {
            configuredProject.IncludeInBuild = false;
            logger.Warning($"Orphaned project: '{configuredProject.FullPath}' identified.");
            this.orphanedProjects.Add(configuredProject);
        }

        public SolutionSearchResult GetSolutionForProject(string projectFilePath, Guid projectGuid) {
            var parser = InitializeSolutionParser();

            ProjectInSolution projectInSolution;
            projectsByGuid.TryGetValue(projectGuid, out projectInSolution);

            string solutionFile;
            projectToSolutionMap.TryGetValue(projectGuid, out solutionFile);

            if (projectInSolution == null || solutionFile == null) {
                string directoryName = Path.GetDirectoryName(projectFilePath);
                var files = Services.FileSystem.GetDirectoryNameOfFilesAbove(directoryName, "*.sln", null);

                foreach (var file in files) {
                    ParseResult parseResult = parser.Parse(file);

                    if (parseResult.ProjectsByGuid.TryGetValue(projectGuid, out projectInSolution)) {

                        projectsByGuid.Add(projectGuid, projectInSolution);
                        projectToSolutionMap.Add(projectGuid, parseResult.SolutionFile);

                        solutionFile = parseResult.SolutionFile;
                        break;
                    }
                }
            }

            if (solutionFile == null) {
                return new SolutionSearchResult { Found = false };
            }

            return new SolutionSearchResult(solutionFile, projectInSolution);
        }

        internal IEnumerable<string> GrovelForFiles(string directory, IReadOnlyCollection<string> excludeFilterPatterns) {
            if (excludeFilterPatterns != null) {
                excludeFilterPatterns = excludeFilterPatterns.Select(PathUtility.GetFullPath).ToList();
            }

            var files = Services.FileSystem.GetFiles(directory, "*.csproj", true);

            foreach (var path in files) {
                bool skip = false;

                if (excludeFilterPatterns != null) {
                    foreach (var pattern in excludeFilterPatterns) {
                        if (WildcardPattern.ContainsWildcardCharacters(pattern)) {
                            WildcardPattern wildcardPattern = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);

                            if (wildcardPattern.IsMatch(path)) {
                                skip = true;
                                break;
                            }
                        }

                        if (path.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0) {
                            skip = true;
                            break;
                        }
                    }
                }

                if (!skip) {
                    yield return path;
                }
            }
        }

        private IEnumerable<IArtifact> CreateBuildDependencyGraphInternal() {
            List<IArtifact> graph = new List<IArtifact>();

            var comparer = DependencyEqualityComparer.Default;

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
                    var exportLifetimeContext = UnconfiguredProjectFactory.CreateExport();
                    var unconfiguredProject = exportLifetimeContext.Value;

                    unconfiguredProject.Initialize(reader, file);

                    loadedUnconfiguredProjects.Add(unconfiguredProject);
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

        Project CreateProject(BuildOperationContext context, AnalysisContext analysisContext, OrchestrationFiles files, DependencyGraph graph);
    }
}
