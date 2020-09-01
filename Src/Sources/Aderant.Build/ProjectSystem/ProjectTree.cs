using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Services;
using Aderant.Build.Utilities;
using Aderant.Build.VersionControl.Model;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Newtonsoft.Json;

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

        private ProjectCollection projectCollection;

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

        public string MetaprojectXml { get; private set; }

        public IReadOnlyCollection<UnconfiguredProject> LoadedUnconfiguredProjects {
            get { return loadedUnconfiguredProjects; }
        }

        public IReadOnlyCollection<ConfiguredProject> LoadedConfiguredProjects {
            get { return loadedConfiguredProjects.Values.ToList(); }
        }

        [Import]
        public IProjectServices Services { get; internal set; }

        public ISolutionManager SolutionManager { get; set; }

        public void LoadProjects(string directory, IReadOnlyCollection<string> excludeFilterPatterns) {
            LoadProjects(new[] { directory }, excludeFilterPatterns);
        }

        public void LoadProjects(IReadOnlyCollection<string> directories, IReadOnlyCollection<string> excludeFilterPatterns, CancellationToken cancellationToken = default(CancellationToken)) {
            UnloadAllProjects();

            EnsureUnconfiguredProjects();

            logger.Info("Raw scanning paths: " + string.Join(",", directories));

            DirectoryGroveler groveler;
            using (PerformanceTimer.Start((duration) => logger.Info("Directory scanning completed in: " + duration.ToString()))) {
                groveler = new DirectoryGroveler(Services.FileSystem);
                groveler.Grovel(directories.ToList(), excludeFilterPatterns?.ToList());
            }

            projectCollection = new ProjectCollection {
                SkipEvaluation = true,
                IsBuildEnabled = false
            };

            ActionBlock<string> parseBlock = new ActionBlock<string>(
                s => LoadAndParseProjectFile(s, null),
                new ExecutionDataflowBlockOptions {
                    MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                });

            foreach (var file in groveler.ProjectFiles) {
                parseBlock.Post(file);
            }

            parseBlock.Complete();

            parseBlock.Completion
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public async Task CollectBuildDependencies(BuildDependenciesCollector collector, CancellationToken cancellationToken = default(CancellationToken)) {

            // Null checked to allow unit testing where projects are inserted directly
            if (LoadedUnconfiguredProjects != null) {
                ErrorUtilities.IsNotNull(collector.ProjectConfiguration, nameof(collector.ProjectConfiguration));

                using (PerformanceTimer.Start(ms => logger?.Info("Loading projects completed in: " + ms.ToString()))) {
                    foreach (var unconfiguredProject in LoadedUnconfiguredProjects) {
                        cancellationToken.ThrowIfCancellationRequested();

                        try {
                            if (!unconfiguredProject.IsTemplateProject()) {
                                ConfiguredProject project = unconfiguredProject.LoadConfiguredProject(this);

                                if (collector.ExtensibilityImposition != null) {
                                    if (collector.ExtensibilityImposition != null) {
                                        project.RequireSynchronizedOutputPaths = collector.ExtensibilityImposition.RequireSynchronizedOutputPaths;
                                    }
                                }

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
            }

            using (PerformanceTimer.Start(ms => logger.Info("Build tree dependencies collected in: " + ms.ToString()))) {
                foreach (var project in LoadedConfiguredProjects) {
                    try {
                        GenerateCurrentSolutionConfigurationXml(collector.ProjectConfiguration);

                        await project.CollectBuildDependencies(collector);

                    } catch (Exception ex) {
                        logger.LogErrorFromException(ex, false, false);
                        throw;
                    }
                }
            }

            UnloadAllProjects();
        }

        public DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector) {
            return CreateBuildDependencyGraph(collector, null);
        }

        public DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector, IBuildPipelineService pipelineService) {
            List<ISourceChange> changes = null;
            if (collector.SourceChanges != null) {
                changes = collector.SourceChanges.ToList();
            }

            foreach (var project in LoadedConfiguredProjects) {
                if (pipelineService != null) {
                    FindRelatedFiles(project.FullPath, pipelineService);
                }

                try {
                    project.AnalyzeBuildDependencies(collector);
                } catch (Exception ex) {
                    logger.LogErrorFromException(ex, false, false);
                    throw;
                }

                if (changes != null) {
                    project.CalculateDirtyStateFromChanges(changes);
                }
            }

            var graph = CreateBuildDependencyGraphInternal();
            return new DependencyGraph(graph);
        }

        public async Task<BuildPlan> ComputeBuildPlan(BuildOperationContext context, AnalysisContext analysisContext, IBuildPipelineService pipelineService, OrchestrationFiles jobFiles) {
            LoadProjects(analysisContext);

            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = context.ConfigurationToBuild;
            collector.ExtensibilityImposition = jobFiles.ExtensibilityImposition;

            await CollectBuildDependencies(collector);

            IReadOnlyCollection<ISourceChange> changes = null;
            if (context.SourceTreeMetadata != null) {
                if (context.SourceTreeMetadata.Changes != null) {
                    // Feed the file changes in so we can calculate the dirty projects.
                    changes = collector.SourceChanges = context.SourceTreeMetadata.Changes;
                }
            }

            //if (changes != null) {
            //    // Because developers may have packaged up their projects the solution level or have just relied on the
            //    // auto packager then there is a risk we will bring files back from the dead in 'DoNotDisableCacheWhenProjectChanged' mode.
            //    // To prevent zombie files we take the slow path and rebuild the entire tree.
            //    bool isAnyNonCSharpFileDeleted = changes.Any(s => (s.Status == FileStatus.Deleted || s.Status == FileStatus.Renamed) && !s.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            //    if (isAnyNonCSharpFileDeleted) {
            //        jobFiles.ExtensibilityImposition.BuildCacheOptions = BuildCacheOptions.DisableCacheWhenProjectChanged;
            //    }
            //}

            DependencyGraph graph = CreateBuildDependencyGraph(collector, pipelineService);

            using (var exportLifetimeContext = SequencerFactory.CreateExport()) {
                var sequencer = exportLifetimeContext.Value;
                sequencer.PipelineService = pipelineService;
                sequencer.MetaprojectXml = this.MetaprojectXml;

                BuildPlan plan = sequencer.CreatePlan(context, jobFiles, graph);
                return plan;
            }
        }

        public void AddConfiguredProject(ConfiguredProject configuredProject) {
            if (configuredProject.IncludeInBuild) {
                ConfiguredProject existing;
                if (loadedConfiguredProjects.TryGetValue(configuredProject.ProjectGuid, out existing)) {
                    throw new DuplicateGuidException(configuredProject.ProjectGuid, $"The project GUID {configuredProject.ProjectGuid} in file {configuredProject.FullPath} has already been assigned to {existing.FullPath}. Duplicate GUIDs are not supported.");
                }

                loadedConfiguredProjects.TryAdd(configuredProject.ProjectGuid, configuredProject);
            } else {
                logger.Info($"The project {configuredProject.FullPath} is not configured to build.");
            }
        }

        public void OrphanProject(ConfiguredProject configuredProject) {
            configuredProject.IncludeInBuild = false;
            logger.Warning($"Orphaned project: '{configuredProject.FullPath}' (not referenced by any solution).");
            this.orphanedProjects.Add(configuredProject);
        }

        public ProjectCollection GetProjectCollection() {
            return ProjectCollection.GlobalProjectCollection;
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

                            // If there a sln next to a workflow project then we need to be careful. We don't want to load the workflow solution
                            // and use that as the solution.
                            if (file.Contains("Workflow")) {
                                if (Services.FileSystem.GetFiles(Path.GetDirectoryName(file), "*.xamlx", false).Any()) {
                                    continue;
                                }
                            }

                            if (projectToSolutionMap.Values.FirstOrDefault(s => string.Equals(s, file, StringComparison.OrdinalIgnoreCase)) != null) {
                                // Already seen this file - project must be orphaned
                                break;
                            }

                            var parser = InitializeSolutionParser();
                            ParseResult parseResult = parser.Parse(file);

                            foreach (var kvp in parseResult.ProjectsByGuid) {
                                if (kvp.Value.ProjectType == SolutionProjectType.SolutionFolder) {
                                    continue;
                                }

                                Guid key = kvp.Key;

                                if (!projectToSolutionMap.ContainsKey(key)) {
                                    projectToSolutionMap.Add(key, parseResult.SolutionFile);
                                } else {
                                    string solutionAlreadyTrackingThisProject = projectToSolutionMap[key];
                                    throw new DuplicateGuidException(key, $"The project guid {key} as part of {file} is already tracked by {solutionAlreadyTrackingThisProject}.");
                                }

                                if (!projectsByGuid.ContainsKey(key)) {
                                    projectsByGuid.Add(key, kvp.Value);
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

        private void UnloadAllProjects() {
            if (projectCollection != null) {
                projectCollection.UnloadAllProjects();
            }

            projectCollection = null;

            GetProjectCollection().UnloadAllProjects();
        }

        internal void FindRelatedFiles(string projectPath, IBuildPipelineService pipelineService) {
            var projectDir = Path.GetDirectoryName(projectPath);

            var manifests = Directory.GetFiles(projectDir, "*RelatedFiles.json", SearchOption.AllDirectories);
            foreach (var manifest in manifests) {
                var text = Services.FileSystem.ReadAllText(manifest);
                var relatedFiles = JsonConvert.DeserializeObject<RelatedFilesManifest>(text).RelatedFiles;
                pipelineService.RecordRelatedFiles(relatedFiles);
            }
        }

        private void GenerateCurrentSolutionConfigurationXml(ConfigurationToBuild collectorProjectConfiguration) {
            var projectsInSolutions = projectsByGuid.Values;

            var generator = new SolutionConfigurationContentsGenerator(collectorProjectConfiguration);
            MetaprojectXml = generator.CreateSolutionProject(projectsInSolutions);
        }

        private void LoadProjects(AnalysisContext analysisContext) {
            EnsureUnconfiguredProjects();

            foreach (string projectFile in analysisContext.ProjectFiles) {
                LoadAndParseProjectFile(projectFile, analysisContext.WixTargetsPath);
            }

            UnconfiguredProject.ClearCaches();
        }

        private void EnsureUnconfiguredProjects() {
            if (loadedUnconfiguredProjects == null) {
                loadedUnconfiguredProjects = new ConcurrentBag<UnconfiguredProject>();
            }
        }

        private IEnumerable<IArtifact> CreateBuildDependencyGraphInternal() {
            List<IArtifact> graph = new List<IArtifact>();

            foreach (var project in LoadedConfiguredProjects) {
                graph.Add(project);
            }

            return graph;
        }


        private SolutionFileParser InitializeSolutionParser() {
            var parser = new SolutionFileParser();
            return parser;
        }

        private void LoadAndParseProjectFile(string file, string wixTargetsPath) {
            using (Stream stream = Services.FileSystem.OpenFile(file)) {
                using (var reader = XmlReader.Create(stream)) {
                    UnconfiguredProject unconfiguredProject;

                    using (var exportLifetimeContext = UnconfiguredProjectFactory.CreateExport()) {
                        unconfiguredProject = exportLifetimeContext.Value;

                        unconfiguredProject.ProjectCollection = projectCollection;
                        unconfiguredProject.AllowConformityModification = true;
                        unconfiguredProject.WixTargetsPath = wixTargetsPath;

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

        /// <summary>
        /// Creates the super project used to run the build.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="files">The extensibility files used to influence the build.</param>
        /// <param name="graph">The projects in the build.</param>
        /// <param name="considerStateFiles">Should the plan use data from the build cache when producing the plan.</param>
        BuildPlan CreatePlan(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph, bool considerStateFiles = true);
    }
}