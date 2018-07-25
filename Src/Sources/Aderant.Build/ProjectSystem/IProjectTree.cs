using System.Collections.Generic;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.MSBuild;
using Aderant.Build.Tasks;

namespace Aderant.Build.ProjectSystem {

    internal interface IProjectTree {
        IReadOnlyCollection<UnconfiguredProject> LoadedUnconfiguredProjects { get; }

        IReadOnlyCollection<ConfiguredProject> LoadedConfiguredProjects { get; }

        IProjectServices Services { get; }

        ISolutionManager SolutionManager { get; }

        Task LoadProjects(string directory, bool recursive, IReadOnlyCollection<string> excludeFilterPatterns);

        /// <summary>
        /// Adds a configured project to this tree.
        /// </summary>
        /// <param name="configuredProject">The configured project.</param>
        void AddConfiguredProject(ConfiguredProject configuredProject);

        /// <summary>
        /// Collects the build dependencies required to build the artifacts in this result.
        /// </summary>
        Task CollectBuildDependencies(BuildDependenciesCollector collector);

        /// <summary>
        /// Analyzes the build dependencies to produce a high level representation of the build order.
        /// </summary>
        DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector);

        Task<Project> ComputeBuildSequence(Context context, AnalysisContext analysisContext, BuildJobFiles jobFiles);
    }
}
