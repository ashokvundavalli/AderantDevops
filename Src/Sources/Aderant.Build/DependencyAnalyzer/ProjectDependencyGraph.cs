using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {

    /// <summary>
    /// A specialized <see cref="DependencyGraph" /> that exposes useful properties
    /// for working with a build tree.
    /// </summary>
    internal class ProjectDependencyGraph : DependencyGraph {
        private ILookup<string, ConfiguredProject> projectsBySolutionRoot;
        private Dictionary<string, HashSet<string>> reverseReferencesMap;

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


        /// <summary>
        /// Reverse dependencies means you want a list of artifacts that depend on a given artifact.
        /// </summary>
        public void ComputeReverseReferencesMap() {
            Dictionary<string, IReadOnlyCollection<IDependable>> referencesMap = Nodes.OfType<IArtifact>().ToDictionary(
                d => d.Id,
                d => d.GetDependencies());

            this.reverseReferencesMap = new Dictionary<string, HashSet<string>>();

            foreach (var kvp in referencesMap) {
                var references = kvp.Value;
                foreach (var referencedId in references) {
                    HashSet<string> reverseReferences;
                    if (!reverseReferencesMap.TryGetValue(referencedId.Id, out reverseReferences)) {
                        reverseReferences = new HashSet<string>();
                        reverseReferencesMap.Add(referencedId.Id, reverseReferences);
                    }

                    reverseReferences.Add(kvp.Key);
                }
            }
        }

        /// <summary>
        /// Gets the list of projects that directly depend on this project.
        /// </summary>
        public IReadOnlyCollection<string> GetProjectsThatDirectlyDependOnThisProject(string projectId) {
            if (projectId == null) {
                throw new ArgumentNullException(nameof(projectId));
            }

            if (reverseReferencesMap == null) {
                ComputeReverseReferencesMap();
            }

            HashSet<string> reverseReferences;
            if (reverseReferencesMap.TryGetValue(projectId, out reverseReferences)) {
                return reverseReferences;
            }

            return new HashSet<string>();
        }
    }
}