using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyAnalyzer {

    internal class DependencyGraph {
        private static IEnumerable<IDependable> emptyDependencies = Enumerable.Empty<IDependable>();
        private readonly IEnumerable<IArtifact> graph;

        public DependencyGraph(IEnumerable<IArtifact> graph) {
            this.graph = graph;
        }

        /// <summary>
        /// Topologically sort artifacts to determine a reasonable order in which they must be built.
        /// </summary>
        public List<IDependable> GetDependencyOrder() {
            var queue = TopologicalSort.Sort<IDependable>(
                graph,
                dep => {
                    var artifact = dep as IArtifact;
                    if (artifact != null) {
                        var dependencies = artifact.GetDependencies();

                        return dependencies;
                    }

                    return emptyDependencies;
                });

            return queue.ToList();
        }

        public List<List<IDependable>> GetBuildGroups(List<IDependable> projects) {
            return GetBuildGroupsInternal(projects);
        }

        private static List<List<IDependable>> GetBuildGroupsInternal(List<IDependable> sortedQueue) {
            // Now find critical path...
            // What we do here is iterate the sorted list looking for elements with no dependencies. These are the zero level modules.
            // Then we iterate again and check if the module depends on any of the zero level modules but not on anything else. These are the
            // level 1 elements. Then we iterate again and check if the module depends on any of the 0 or 1 level modules etc.
            // This places modules into levels which allows for maximum parallelism based on dependency.
            IDictionary<int, HashSet<IDependable>> levels = new Dictionary<int, HashSet<IDependable>>();

            Queue<IDependable> projects = new Queue<IDependable>(sortedQueue);

            int i = 0;
            while (projects.Count > 0) {
                IDependable project = projects.Peek();

                if (!levels.ContainsKey(i)) {
                    levels[i] = new HashSet<IDependable>();
                }

                bool add = true;

                var artifact = project as IArtifact;
                if (artifact != null) {
                    IReadOnlyCollection<IDependable> dependencies = artifact.GetDependencies();

                    var levelSet = levels[i];
                    foreach (var item in levelSet) {
                        if (dependencies.Contains(item)) {
                            add = false;
                        }
                    }
                }

                if (add) {
                    levels[i].Add(project);
                    projects.Dequeue();
                } else {
                    i++;
                }
            }

            var groups = new List<List<IDependable>>();
            foreach (var pair in levels) {
                groups.Add(new List<IDependable>(levels[pair.Key]));
            }

            return groups;
        }
    }
}
