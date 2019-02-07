using System;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {

    /// <summary>
    /// A specialized <see cref="DependencyGraph"/> that exposes useful properties
    /// for working with a build tree.
    /// </summary>
    internal class ProjectDependencyGraph : DependencyGraph {
        private ILookup<string, ConfiguredProject> projectsBySolutionRoot;

        public ProjectDependencyGraph(DependencyGraph graph)
            : base(graph.Nodes) {
        }

        public ProjectDependencyGraph(params IDependable[] graph)
            : base(graph) {
        }

        public ILookup<string, ConfiguredProject> ProjectsBySolutionRoot {
            get {
                return projectsBySolutionRoot ?? (projectsBySolutionRoot = Nodes
                           .OfType<ConfiguredProject>()
                           .ToLookup(g => g.SolutionRoot, g => g, StringComparer.OrdinalIgnoreCase));
            }
        }

        public override void Add(IArtifact artifact) {
            base.Add(artifact);

            projectsBySolutionRoot = null;
        }
    }

}