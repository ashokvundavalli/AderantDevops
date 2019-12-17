using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyAnalyzer {

    internal class DependencyGraph {
        private readonly List<IDependable> artifacts;

        public DependencyGraph(IEnumerable<IDependable> artifacts) {
            this.artifacts = artifacts.ToList();
        }

        public IReadOnlyCollection<IDependable> Nodes {
            get { return artifacts; }
        }

        /// <summary>
        /// Topologically sort artifacts to determine a reasonable order in which they must be built.
        /// </summary>
        public IReadOnlyList<IDependable> GetDependencyOrder() {
            return Graph.TopologicalSort(artifacts, GetDependencies);
        }

        private static IEnumerable<IDependable> GetDependencies(IDependable input) {
            if (input is IArtifact artifact) {
                return artifact.GetDependencies();
            }

            return Enumerable.Empty<IDependable>();
        }

        /// <summary>
        /// Groups the input into sets that do not depend on each other.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<IDependable>> GetBuildGroups() {
            return GetBuildGroups(artifacts);
        }

        /// <summary>
        /// Groups the input into sets that do not depend on each other.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<IDependable>> GetBuildGroups(IEnumerable<IDependable> projects) {
            // Now find critical path...
            // What we do here is iterate the sorted list looking for elements with no dependencies. These are the zero level modules.
            // Then we iterate again and check if the module depends on any of the zero level modules but not on anything else. These are the
            // level 1 elements. Then we iterate again and check if the module depends on any of the 0 or 1 level modules etc.
            // This places modules into levels which partitioning the groups to gain execution parallelism.
            return Graph.BatchingTopologicalSort(projects, GetDependencies);
        }

        /// <summary>
        /// Adds a new artifact to the container.
        /// </summary>
        public virtual void Add(IArtifact artifact) {
            artifacts.Add(artifact);
        }

        public T GetNodeById<T>(string id) where T : class, IArtifact {
            return Nodes.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) as T;
        }
    }
}