using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aderant.Build.ProjectSystem {

    internal interface IProjectTree {
        IReadOnlyCollection<UnconfiguredProject> LoadedUnconfiguredProjects { get; }

        IReadOnlyCollection<ConfiguredProject> LoadedConfiguredProjects { get; }

        IProjectServices Services { get; }

        ISolutionManager SolutionManager { get; }

        Task LoadProjectsAsync(string directory, bool recursive);

        /// <summary>
        /// Collects the build dependencies required to build the artifacts in this result.
        /// </summary>
        Task CollectBuildDependencies(BuildDependenciesCollector buildDependenciesCollector);

        void AddConfiguredProject(ConfiguredProject configuredProject);
    }

}
