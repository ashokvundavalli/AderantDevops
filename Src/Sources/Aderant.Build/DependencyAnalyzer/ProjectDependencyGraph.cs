using System;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ProjectDependencyGraph : DependencyGraph {
        private ILookup<string, ConfiguredProject> grouping;

        public ProjectDependencyGraph(DependencyGraph graph) : base(graph.Nodes) {
        }

        public ProjectDependencyGraph(params IDependable[] graph)
            : base(graph) {
        }

        public override void Add(IArtifact artifact) {
            base.Add(artifact);

            grouping = null;
        }

        public ILookup<string, ConfiguredProject> ProjectsBySolutionRoot {
            get {
                return grouping ?? (grouping = Nodes
                           .OfType<ConfiguredProject>()
                           .ToLookup(g => g.SolutionRoot, g => g, StringComparer.OrdinalIgnoreCase));
            }
        }
    }
}