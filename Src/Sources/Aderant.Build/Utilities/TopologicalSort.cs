using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Utilities {

    internal static class TopologicalSortExtensions {
        public static IReadOnlyList<TNode> TopologicalSort<TNode>(this IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> successors) {
            return Aderant.Build.Utilities.TopologicalSort.IterativeSort(nodes, successors);
        }
    }

    /// <summary>
    /// A helper class that contains a topological sort algorithm.
    /// </summary>
    internal static class TopologicalSort {

        /// <summary>
        /// Produce a topological sort of a given directed acyclic graph.
        /// </summary>
        /// <typeparam name="TNode">The type of the node</typeparam>
        /// <param name="nodes">Any subset of the nodes that includes all nodes with no predecessors</param>
        /// <param name="successors">A function mapping a node to its set of successors</param>
        /// <returns>A list of all reachable nodes, in which each node always precedes its successors</returns>
        /// <remarks>Uplifted from the Roslyn compiler project</remarks>
        public static IReadOnlyList<TNode> IterativeSort<TNode>(IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> successors) {
            // First, count the predecessors of each node
            IReadOnlyCollection<TNode> allNodes;
            var predecessorCounts = PredecessorCounts(nodes, successors, out allNodes);

            // Initialize the ready set with those nodes that have no predecessors
            var ready = new Stack<TNode>(predecessorCounts.Count);
            foreach (TNode node in allNodes) {
                if (predecessorCounts[node] == 0) {
                    ready.Push(node);
                }
            }

            // Process the ready set. Output a node, and decrement the predecessor count of its successors.
            var resultBuilder = new List<TNode>(predecessorCounts.Count);
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
                var unsortedNodes = predecessorCounts.Keys.Except(resultBuilder);
                throw new ArgumentException("FATAL: Cycle in dependency graph.");
            }

            resultBuilder.Reverse();

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
                    int succPredecessorCount;
                    if (predecessorCounts.TryGetValue(succ, out succPredecessorCount)) {
                        predecessorCounts[succ] = succPredecessorCount + 1;
                    } else {
                        predecessorCounts.Add(succ, 1);
                    }
                }
            }

            allNodes = allNodesBuilder;
            return predecessorCounts;
        }
    }
}