﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
using Aderant.Build.VersionControl.Model;
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

        private ProjectToSolutionMap projectToSolutionMap = new ProjectToSolutionMap();


        public ProjectTree() {

        }

        [ImportingConstructor]
        public ProjectTree(ILogger logger) {
            this.logger = logger;
            SolutionManager = this;
        }

        internal ProjectTree(IEnumerable<UnconfiguredProject> unconfiguredProjects)
            : this(NullLogger.Default) {

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

            if (changes != null && changes.Count > 0) {
                collector.UnreconciledChanges = changes.ToList();
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

            if (context.SourceTreeMetadata != null) {
                if (context.SourceTreeMetadata.Changes != null) {
                    // Feed the file changes in so we can calculate the dirty projects.
                    collector.SourceChanges = context.SourceTreeMetadata.Changes;
                }
            }

            DependencyGraph graph = CreateBuildDependencyGraph(collector, pipelineService);

            EvictStateFilesForConesWithUnreconciledChanges(context, collector);

            using (var exportLifetimeContext = SequencerFactory.CreateExport()) {
                var sequencer = exportLifetimeContext.Value;

                sequencer.PipelineService = pipelineService;
                sequencer.MetaprojectXml = this.MetaprojectXml;

                BuildPlan plan = sequencer.CreatePlan(context, jobFiles, graph, true, this);
                return plan;
            }
        }

        /// <summary>
        /// Removes state files that are for cones with unreconciled changes to ensure that the projects
        /// in that cone are scheduled to build. We cannot tell what the side effects of not building the projects are
        /// so its safer to schedule them.
        /// </summary>
        private void EvictStateFilesForConesWithUnreconciledChanges(BuildOperationContext context, BuildDependenciesCollector collector) {
            if (context.BuildStateMetadata != null) {
                if (collector.UnreconciledChanges.Count > 0) {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("The following changes are not reconciled to a project");
                    foreach (var file in collector.UnreconciledChanges) {
                        sb.Append(Constants.LoggingArrow);
                        sb.AppendLine(file.Path);
                    }

                    logger.Info(sb.ToString());

                    var roots = collector.UnreconciledChanges.Select(s => PathUtility.GetRootDirectory(s.Path)).Distinct(StringComparer.OrdinalIgnoreCase);
                    context.BuildStateMetadata.RemoveStateFilesForRoots(roots);
                }
            }
        }

        public void AddConfiguredProject(ConfiguredProject configuredProject) {
            if (configuredProject.IncludeInBuild) {
                // Check for duplicate GUIDs amongst existing projects.
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
            var projectInSolution = projectToSolutionMap.GetProjectInSolution(projectFilePath);
            var solutionFile = projectToSolutionMap.GetSolution(projectInSolution);

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

                            if (projectToSolutionMap.HasSeenFile(file)) {
                                // Already seen this file - project must be orphaned
                                break;
                            }

                            var parser = InitializeSolutionParser();
                            ParseResult parseResult = parser.Parse(file);

                            if (RetrieveFirstProjectMatch(projectFilePath, projectGuid, parseResult, file, exactMatch, out projectInSolution)) {
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

        private bool RetrieveFirstProjectMatch(string projectFilePath, Guid projectGuid, ParseResult parseResult, string possibleSolutionFile, string exactMatch, out ProjectInSolutionWrapper projectInSolution) {
            var project = parseResult.ProjectsInOrder.FirstOrDefault(s => string.Equals(s.AbsolutePath, projectFilePath, StringComparison.OrdinalIgnoreCase));
            if (project != null) {
                projectToSolutionMap.AddProject(projectFilePath, projectGuid, parseResult.SolutionFile, project);
            }

            // Populate the cache with all project data from the solution, we don't actually know if the project GUIDs are correct here
            foreach (var wrapper in parseResult.ProjectsInOrder) {
                var parsed = Guid.Parse(wrapper.ProjectGuid);
                if (parsed == projectGuid) {
                    continue;
                }

                projectToSolutionMap.AddProject(wrapper.AbsolutePath, parsed, parseResult.SolutionFile, wrapper);
            }

            var result = projectToSolutionMap.GetProjectInSolution(projectFilePath);
            if (result != null) {

                if (exactMatch != null) {
                    logger.Info($"Found exact solution match for project {projectFilePath} -> {exactMatch}");
                } else {
                    logger.Info($"Found inexact solution match for project {projectFilePath} -> {possibleSolutionFile}");
                }

                projectInSolution = result;
                return true;
            }

            projectInSolution = null;
            return false;
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
            var generator = new SolutionConfigurationContentsGenerator(collectorProjectConfiguration);
            MetaprojectXml = generator.CreateSolutionProject(projectToSolutionMap.AllProjects);
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
        /// <param name="SdkProjects"></param>
        BuildPlan CreatePlan(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph, bool considerStateFiles, IProjectTree SdkProjects);
    }
}
