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
        private Dictionary<string, ConfiguredProject> projectMap;

        public ProjectDependencyGraph(DependencyGraph graph)
            : base(graph.Nodes) {
        }

        public ProjectDependencyGraph(params IDependable[] graph)
            : base(graph) {
        }

        /// <summary>
        /// All projects grouped by their directory root. Lazily computed.
        /// </summary>
        public ILookup<string, ConfiguredProject> ProjectsBySolutionRoot {
            get {
                return projectsBySolutionRoot ?? (projectsBySolutionRoot = Projects.ToLookup(g => g.SolutionRoot, g => g, StringComparer.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// All projects within the graph. Lazily computed.
        /// </summary>
        public IReadOnlyCollection<ConfiguredProject> Projects {
            get {
                ComputeProjectMap();
                return projectMap.Values;
            }
        }

        private void ComputeProjectMap() {
            if (projectMap == null) {
                Dictionary<string, ConfiguredProject> dictionary = new Dictionary<string, ConfiguredProject>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in Nodes) {
                    var project = node as ConfiguredProject;
                    if (project != null) {
                        dictionary.Add(project.Id, project);
                    }
                }

                projectMap = dictionary;
            }
        }

        public override void Add(IArtifact artifact) {
            base.Add(artifact);

            projectsBySolutionRoot = null;
            projectMap = null;
        }

        /// <summary>
        /// Reverse dependencies means you want a list of artifacts that depend on a given artifact.
        /// </summary>
        public void ComputeReverseReferencesMap() {
            Dictionary<string, IReadOnlyCollection<IDependable>> referencesMap = Nodes.OfType<IArtifact>().ToDictionary(
                d => d.Id,
                d => d.GetDependencies());

            reverseReferencesMap = new Dictionary<string, HashSet<string>>();

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

        public ConfiguredProject GetProject(string id) {
            ComputeProjectMap();

            ConfiguredProject project;
            projectMap.TryGetValue(id, out project);

            return project;
        }
    }
}