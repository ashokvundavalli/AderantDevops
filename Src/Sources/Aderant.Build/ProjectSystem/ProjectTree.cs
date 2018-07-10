﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
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

        private ConcurrentBag<UnconfiguredProject> loadedUnconfiguredProjects;

        // Holds any projects which we cannot load a solution for
        private ConcurrentBag<ConfiguredProject> orphanedProjects = new ConcurrentBag<ConfiguredProject>();

        // First level cache for parsed solution information
        private Dictionary<Guid, ProjectInSolution> projectsByGuid = new Dictionary<Guid, ProjectInSolution>();
        private Dictionary<Guid, string> projectToSolutionMap = new Dictionary<Guid, string>();

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
        public IProjectServices Services { get; private set; }

        public ISolutionManager SolutionManager {
            get { return this; }
        }

        public Task LoadProjects(string directory, bool recursive, IReadOnlyCollection<string> excludeFilterPatterns) {
            if (loadedUnconfiguredProjects == null) {
                loadedUnconfiguredProjects = new ConcurrentBag<UnconfiguredProject>();
            }

            var files = GrovelForFiles(directory, excludeFilterPatterns);

            return Task.WhenAll(files.Select(file => Task.Run(() => LoadAndParseProjectFile(file))).ToArray());
        }

        private IEnumerable<string> GrovelForFiles(string directory, IReadOnlyCollection<string> excludeFilterPatterns) {
            var files = Services.FileSystem.GetFiles(directory, "*.csproj", true);

            foreach (var path in files) {
                bool skip = false;
                if (excludeFilterPatterns != null) {
                    foreach (var pattern in excludeFilterPatterns) {
                        if (path.Contains(pattern)) {
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

        public async Task CollectBuildDependencies(BuildDependenciesCollector collector) {
            foreach (var unconfiguredProject in LoadedUnconfiguredProjects) {
                ConfiguredProject project = unconfiguredProject.LoadConfiguredProject();

                project.AssignProjectConfiguration(collector.ProjectConfiguration);
            }

            foreach (var project in LoadedConfiguredProjects) {
                await project.CollectBuildDependencies(collector);
            }
        }

        public DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector) {
            foreach (var project in LoadedConfiguredProjects) {
                project.AnalyzeBuildDependencies(collector);
            }

            var graph = BuildDependencyGraph();
            return new DependencyGraph(graph);
        }

        public async Task<Project> GenerateBuildJob(Context context, AnalysisContext analysisContext, BuildJobFiles instance) {
            await LoadProjects(context.BuildRoot.FullName, true, analysisContext.ExcludePaths);

            var collector = new BuildDependenciesCollector();
            await CollectBuildDependencies(collector);

            DependencyGraph graph = CreateBuildDependencyGraph(collector);

            using (var exportLifetimeContext = this.SequencerFactory.CreateExport()) {
                var sequencer = exportLifetimeContext.Value;

                Project project = sequencer.CreateProject(context, instance, graph);
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

        private IEnumerable<IArtifact> BuildDependencyGraph() {
            List<IArtifact> graph = new List<IArtifact>();

            var comparer = DependencyEqualityComparer.Default;

            HashSet<IDependable> allDependencies = new HashSet<IDependable>(comparer);

            ProcessProjects(allDependencies, comparer, graph);
            AddInitializeAndCompletionNodes(graph);

            return graph;
        }

        private void AddInitializeAndCompletionNodes(List<IArtifact> graph) {
            var grouping = graph
                .OfType<ConfiguredProject>()
                .GroupBy(g => Path.GetDirectoryName(g.SolutionFile), StringComparer.OrdinalIgnoreCase);

            foreach (var level in grouping) {
                IArtifact initializeNode;
                string solutionDirectoryName = Path.GetFileName(level.Key);

                // Create a new node that represents the start of a directory
                graph.Add(initializeNode = new DirectoryNode(solutionDirectoryName, level.Key, false));

                // Create a new node that represents the completion of a directory
                DirectoryNode completionNode = new DirectoryNode(solutionDirectoryName, level.Key, true);

                graph.Add(completionNode);
                completionNode.AddResolvedDependency(null, initializeNode);

                foreach (var project in graph
                    .OfType<ConfiguredProject>()
                    .Where(p => string.Equals(Path.GetDirectoryName(p.SolutionFile), level.Key, StringComparison.OrdinalIgnoreCase))) {

                    project.AddResolvedDependency(null, initializeNode);
                    completionNode.AddResolvedDependency(null, project);
                }
            }
        }

        private void ProcessProjects(HashSet<IDependable> allDependencies, IEqualityComparer<IDependable> comparer, List<IArtifact> graph) {
            foreach (var project in LoadedConfiguredProjects) {
                project.ReplaceDependencies(allDependencies, comparer);

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

        public static IProjectTree CreateDefaultImplementation(IReadOnlyCollection<Assembly> assemblies, bool throwCompositionErrors = false) {
            return GetProjectService(CreateSelfHostContainer(assemblies, throwCompositionErrors));
        }

        private static IProjectTree GetProjectService(ServiceContainer createSelfHostContainer) {
            return createSelfHostContainer.GetExportedValue<IProjectTree>();
        }

        private static ServiceContainer CreateSelfHostContainer(IReadOnlyCollection<Assembly> assemblies, bool throwCompositionErrors) {
            return new ServiceContainer(assemblies);
        }

        /// <summary>
        /// Creates the default project tree implementation.
        /// </summary>
        /// <returns>IProjectTree.</returns>
        public static IProjectTree CreateDefaultImplementation() {
            return CreateDefaultImplementation(new[] { typeof(ProjectTree).Assembly });
        }
    }

    internal interface ISequencer {
        Project CreateProject(Context context, BuildJobFiles files, DependencyGraph graph);
    }
}
