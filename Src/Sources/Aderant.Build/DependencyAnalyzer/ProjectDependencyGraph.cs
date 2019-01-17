using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ProjectDependencyGraph : DependencyGraph {
        private ILookup<string, ConfiguredProject> grouping;

        public ProjectDependencyGraph(DependencyGraph graph)
            : base(graph.Nodes) {
        }

        public ProjectDependencyGraph(params IDependable[] graph)
            : base(graph) {
        }

        public ILookup<string, ConfiguredProject> ProjectsBySolutionRoot {
            get {
                return grouping ?? (grouping = Nodes
                           .OfType<ConfiguredProject>()
                           .ToLookup(g => g.SolutionRoot, g => g, StringComparer.OrdinalIgnoreCase));
            }
        }

        public override void Add(IArtifact artifact) {
            base.Add(artifact);

            grouping = null;
        }
    }

    internal static class TopologicalSort {

        public static IReadOnlyCollection<TNode> IterativeSort<TNode>(IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> successors) {
            // First, count the predecessors of each node
            var predecessorCounts = PredecessorCounts(nodes, successors, out IReadOnlyCollection<TNode> allNodes);

            // Initialize the ready set with those nodes that have no predecessors
            var ready = new Stack<TNode>();
            foreach (TNode node in allNodes) {
                if (predecessorCounts[node] == 0) {
                    ready.Push(node);
                }
            }

            // Process the ready set. Output a node, and decrement the predecessor count of its successors.
            var resultBuilder = new List<TNode>();
            while (ready.Count != 0) {
                var node = ready.Pop();
                resultBuilder.Add(node);
                foreach (var succ in successors(node)) {
                    var count = predecessorCounts[succ];
                    Debug.Assert(count != 0);
                    predecessorCounts[succ] = count - 1;
                    if (count == 1) {
                        ready.Push(succ);
                    }
                }
            }

            // At this point all the nodes should have been output, otherwise there was a cycle
            if (predecessorCounts.Count != resultBuilder.Count) {
                throw new ArgumentException("Cycle in the input graph");
            }

            //predecessorCounts.Free();
            //ready.Free();
            return resultBuilder;
        }

        private static Dictionary<TNode, int> PredecessorCounts<TNode>(
            IEnumerable<TNode> nodes,
            Func<TNode, IEnumerable<TNode>> successors,
            out IReadOnlyCollection<TNode> allNodes) {
            var predecessorCounts = new Dictionary<TNode, int>();
            var counted = new HashSet<TNode>();
            var toCount = new Stack<TNode>(nodes);
            var allNodesBuilder = new List<TNode>();
            //toCount.AddRange(nodes);

            while (toCount.Count != 0) {
                var n = toCount.Pop();
                if (!counted.Add(n)) {
                    continue;
                }

                allNodesBuilder.Add(n);
                if (!predecessorCounts.ContainsKey(n)) {
                    predecessorCounts.Add(n, 0);
                }

                foreach (var succ in successors(n)) {
                    toCount.Push(succ);
                    if (predecessorCounts.TryGetValue(succ, out int succPredecessorCount)) {
                        predecessorCounts[succ] = succPredecessorCount + 1;
                    } else {
                        predecessorCounts.Add(succ, 1);
                    }
                }
            }

            //counted.Free();
            //toCount.Free();
            allNodes = allNodesBuilder; //.ToImmutableAndFree();
            return predecessorCounts;
        }
    }
}