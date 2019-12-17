using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.PipelineService;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {

    internal interface IProjectTree {
        IReadOnlyCollection<UnconfiguredProject> LoadedUnconfiguredProjects { get; }

        IReadOnlyCollection<ConfiguredProject> LoadedConfiguredProjects { get; }

        IProjectServices Services { get; }

        ISolutionManager SolutionManager { get; }

        /// <summary>
        /// Convenience method.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="excludeFilterPatterns"></param>
        void LoadProjects(string directory, IReadOnlyCollection<string> excludeFilterPatterns);

        void LoadProjects(IReadOnlyCollection<string> directories, IReadOnlyCollection<string> excludeFilterPatterns, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Adds a configured project to this tree.
        /// </summary>
        /// <param name="configuredProject">The configured project.</param>
        void AddConfiguredProject(ConfiguredProject configuredProject);

        /// <summary>
        /// Collects the build dependencies required to build the artifacts in this result.
        /// </summary>
        Task CollectBuildDependencies(BuildDependenciesCollector collector, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Analyzes the build dependencies to produce a high level representation of the build order.
        /// </summary>
        DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector);

        DependencyGraph CreateBuildDependencyGraph(BuildDependenciesCollector collector, IBuildPipelineService pipelineService);

        Task<BuildPlan> ComputeBuildPlan(BuildOperationContext context, AnalysisContext analysisContext, IBuildPipelineService pipelineService, OrchestrationFiles jobFiles);
    }
}
