using System.Collections.Generic;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// A <see cref="DependencyGraph"/> models the relationship between projects in a solution, and projects and their external dependencies.
    /// </summary>
    internal class DependencyGraph {
        private IEnumerable<IDependencyRef> ordered;

        public DependencyGraph(IEnumerable<IDependencyRef> ordered) {
            this.ordered = ordered;
        }

        public List<IDependencyRef> GetDependencyOrder() {
            var queue = TopologicalSort.Sort(ordered, dep => dep.DependsOn);

            // TODO
            //if (!ordered.Sort(out queue)) {
            //    DetectCircularDependencies(queue);
            //    throw new CircularDependencyException(queue.Select(s => s.Name));
            //}

            return queue.ToList();
        }

        internal List<IDependencyRef> DetectCircularDependencies(Queue<IDependencyRef> queue) {
            IEnumerable<string> moduleNames = queue.Select(s => s.Name);
            List<IDependencyRef> dependencies = new List<IDependencyRef>();

            foreach (IDependencyRef module in queue) {
                List<IDependencyRef> dependencyReferences = module.DependsOn.Select(x => x).Where(y => moduleNames.Contains(y.Name)).ToList();

                if (dependencyReferences.Any()) {
                    dependencies.Add(
                        new ExpertModule(module.Name) {
                            DependsOn = dependencyReferences
                        });
                }
            }

            return dependencies;
        }

        public List<List<IDependencyRef>> GetBuildGroups(IEnumerable<IDependencyRef> projects) {
            return GetBuildGroupsInternal(projects);
        }

        private static List<List<IDependencyRef>> GetBuildGroupsInternal(IEnumerable<IDependencyRef> sortedQueue) {
            // Now find critical path...
            // What we do here is iterate the sorted list looking for elements with no dependencies. These are the zero level modules.
            // Then we iterate again and check if the module depends on any of the zero level modules but not on anything else. These are the
            // level 1 elements. Then we iterate again and check if the module depends on any of the 0 or 1 level modules etc.
            // This places modules into levels which allows for maximum parallelism based on dependency.
            IDictionary<int, HashSet<IDependencyRef>> levels = new Dictionary<int, HashSet<IDependencyRef>>();

            Queue<IDependencyRef> projects = new Queue<IDependencyRef>(sortedQueue);

            int i = 0;
            while (projects.Count > 0) {
                IDependencyRef project = projects.Peek();

                if (!levels.ContainsKey(i)) {
                    levels[i] = new HashSet<IDependencyRef>();
                }

                bool add = true;

                if (project.DependsOn != null) {
                    var levelSet = levels[i];
                    foreach (var item in levelSet) {
                        if (project.DependsOn.Contains(item)) {
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

            List<List<IDependencyRef>> groups = new List<List<IDependencyRef>>();
            foreach (KeyValuePair<int, HashSet<IDependencyRef>> pair in levels) {
                groups.Add(new List<IDependencyRef>(levels[pair.Key]));
            }

            return groups;
        }
    }
}
