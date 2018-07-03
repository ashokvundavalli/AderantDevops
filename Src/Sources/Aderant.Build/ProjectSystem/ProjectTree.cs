using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.Services;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(IProjectTree))]
    internal class ProjectTree : IProjectTree, ISolutionManager {
        // Holds all projects that are applicable to the build tree
        private readonly ConcurrentBag<ConfiguredProject> loadedConfiguredProjects = new ConcurrentBag<ConfiguredProject>();

        private ConcurrentBag<UnconfiguredProject> loadedUnconfiguredProjects;

        // First level cache for parsed solution information
        private Dictionary<Guid, ProjectInSolution> projectsByGuid = new Dictionary<Guid, ProjectInSolution>();
        private Dictionary<Guid, string> projectToSolutionMap = new Dictionary<Guid, string>();

        [Import]
        private ExportFactory<UnconfiguredProject> UnconfiguredProjectFactory { get; set; }

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

        public Task LoadProjectsAsync(string directory, bool recursive) {
            var files = Services.FileSystem.GetFiles(directory, "*.csproj", true);

            if (loadedUnconfiguredProjects == null) {
                loadedUnconfiguredProjects = new ConcurrentBag<UnconfiguredProject>();
            }

            return Task.WhenAll(
                files.Select(file => Task.Run(() => LoadAndParseProjectFile(file))).ToArray());
        }

        public async Task CollectBuildDependencies(BuildDependenciesCollector collector) {
            foreach (var unconfiguredProject in LoadedUnconfiguredProjects) {
                ConfiguredProject project = unconfiguredProject.LoadConfiguredProject("");

                project.AssignProjectConfiguration("Debug|Any CPU");
            }

            foreach (var project in LoadedConfiguredProjects) {
                await project.CollectBuildDependencies(collector);
            }
        }
        public void AnalyzeBuildDependencies(BuildDependenciesCollector collector) {
            foreach (var project in LoadedConfiguredProjects) {
                project.AnalyzeBuildDependencies(collector);
            }
        }

        public void AddConfiguredProject(ConfiguredProject configuredProject) {
            if (configuredProject.IncludeInBuild) {
                loadedConfiguredProjects.Add(configuredProject);
            }
        }

        public SolutionProject GetSolutionForProject(string projectFilePath, Guid projectGuid) {
            var parser = InitializeSolutionParser();

            ProjectInSolution projectInSolution;
            projectsByGuid.TryGetValue(projectGuid, out projectInSolution);

            string solutionFile;
            projectToSolutionMap.TryGetValue(projectGuid, out solutionFile);

            if (projectInSolution == null || solutionFile == null) {
                string directoryName = Path.GetDirectoryName(projectFilePath);
                var files = Services.FileSystem.GetDirectoryNameOfFilePatternAbove(directoryName, "*.sln", null);

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

            return new SolutionProject(solutionFile, projectInSolution);
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
}
