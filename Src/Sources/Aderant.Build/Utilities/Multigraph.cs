﻿using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Annotations;

namespace Aderant.Build.Utilities {


    /// <summary>
    /// A topological sort algorthim
    /// </summary>
    /// <remarks>
    /// Copyright (c) .NET Foundation. All rights reserved.
    /// Licensed under the Apache License, Version 2.0.
    /// </remarks>
    internal class Multigraph<TVertex, TEdge> : Graph<TVertex> {
        private readonly HashSet<TVertex> vertices = new HashSet<TVertex>();
        private readonly HashSet<TEdge> edges = new HashSet<TEdge>();
        private readonly Dictionary<TVertex, Dictionary<TVertex, List<TEdge>>> successorMap = new Dictionary<TVertex, Dictionary<TVertex, List<TEdge>>>();

        public virtual IEnumerable<TEdge> Edges {
            get { return edges; }
        }

        public virtual IEnumerable<TEdge> GetEdges([NotNull] TVertex from, [NotNull] TVertex to) {
            Dictionary<TVertex, List<TEdge>> successorSet;
            if (successorMap.TryGetValue(from, out successorSet)) {
                List<TEdge> edgeList;
                if (successorSet.TryGetValue(to, out edgeList)) {
                    return edgeList;
                }
            }

            return Enumerable.Empty<TEdge>();
        }

        public virtual bool AddVertex([NotNull] TVertex vertex) {
            return vertices.Add(vertex);
        }

        public virtual void AddVertices([NotNull] IEnumerable<TVertex> vertices) {
            this.vertices.UnionWith(vertices);
        }

        public virtual void AddEdge([NotNull] TVertex from, [NotNull] TVertex to, [NotNull] TEdge edge) {
            AddEdges(@from, to, new[] { edge });
        }

        public virtual void AddEdges([NotNull] TVertex from, [NotNull] TVertex to, [NotNull] IEnumerable<TEdge> edges) {
            if (!vertices.Contains(from)) {
                throw new InvalidOperationException("Graph does not contain vertex: " + from);
            }

            if (!vertices.Contains(to)) {
                throw new InvalidOperationException("Graph does not contain vertex: " + to);
            }

            Dictionary<TVertex, List<TEdge>> successorSet;
            if (!successorMap.TryGetValue(from, out successorSet)) {
                successorSet = new Dictionary<TVertex, List<TEdge>>();
                successorMap.Add(from, successorSet);
            }

            List<TEdge> edgeList;
            if (!successorSet.TryGetValue(to, out edgeList)) {
                edgeList = new List<TEdge>();
                successorSet.Add(to, edgeList);
            }

            edgeList.AddRange(edges);
            this.edges.UnionWith(edgeList);
        }

        public virtual IReadOnlyList<TVertex> TopologicalSort([CanBeNull] Func<TVertex, TVertex, IEnumerable<TEdge>, bool> canBreakEdge) {
            return TopologicalSort(canBreakEdge, null);
        }

        public override IReadOnlyList<TVertex> TopologicalSort() {
            return TopologicalSort(null, null);
        }

        public virtual IReadOnlyList<TVertex> TopologicalSort(
            [CanBeNull] Func<TVertex, TVertex, IEnumerable<TEdge>, bool> canBreakEdge,
            [CanBeNull] Func<IEnumerable<Tuple<TVertex, TVertex, IEnumerable<TEdge>>>, string> formatCycle) {
            var sortedQueue = new List<TVertex>();
            var predecessorCounts = new Dictionary<TVertex, int>();

            foreach (var vertex in vertices) {
                foreach (var outgoingNeighbour in GetOutgoingNeighbours(vertex)) {
                    if (predecessorCounts.ContainsKey(outgoingNeighbour)) {
                        predecessorCounts[outgoingNeighbour]++;
                    } else {
                        predecessorCounts[outgoingNeighbour] = 1;
                    }
                }
            }

            foreach (var vertex in vertices) {
                if (!predecessorCounts.ContainsKey(vertex)) {
                    sortedQueue.Add(vertex);
                }
            }

            var index = 0;
            while (sortedQueue.Count < vertices.Count) {
                while (index < sortedQueue.Count) {
                    var currentRoot = sortedQueue[index];

                    foreach (var successor in GetOutgoingNeighbours(currentRoot).Where(neighbour => predecessorCounts.ContainsKey(neighbour))) {
                        // Decrement counts for edges from sorted vertices and append any vertices that no longer have predecessors
                        predecessorCounts[successor]--;
                        if (predecessorCounts[successor] == 0) {
                            sortedQueue.Add(successor);
                            predecessorCounts.Remove(successor);
                        }
                    }

                    index++;
                }

                // Cycle breaking
                if (sortedQueue.Count < vertices.Count) {
                    var broken = false;

                    var candidateVertices = predecessorCounts.Keys.ToList();
                    var candidateIndex = 0;

                    // Iterate over the unsorted vertices
                    while ((candidateIndex < candidateVertices.Count)
                           && !broken
                           && (canBreakEdge != null)) {
                        var candidateVertex = candidateVertices[candidateIndex];

                        // Find vertices in the unsorted portion of the graph that have edges to the candidate
                        var incomingNeighbours = GetIncomingNeighbours(candidateVertex)
                            .Where(neighbour => predecessorCounts.ContainsKey(neighbour)).ToList();

                        foreach (var incomingNeighbour in incomingNeighbours) {
                            // Check to see if the edge can be broken
                            if (canBreakEdge(incomingNeighbour, candidateVertex, successorMap[incomingNeighbour][candidateVertex])) {
                                predecessorCounts[candidateVertex]--;
                                if (predecessorCounts[candidateVertex] == 0) {
                                    sortedQueue.Add(candidateVertex);
                                    predecessorCounts.Remove(candidateVertex);
                                    broken = true;
                                    break;
                                }
                            }
                        }

                        candidateIndex++;
                    }

                    if (!broken) {
                        // Failed to break the cycle
                        var currentCycleVertex = vertices.First(v => predecessorCounts.ContainsKey(v));
                        var cycle = new List<TVertex> { currentCycleVertex };
                        var finished = false;
                        while (!finished) {
                            // Find a cycle
                            foreach (var predecessor in GetIncomingNeighbours(currentCycleVertex)
                                .Where(neighbour => predecessorCounts.ContainsKey(neighbour))) {
                                if (predecessorCounts[predecessor] != 0) {
                                    predecessorCounts[currentCycleVertex] = -1;

                                    currentCycleVertex = predecessor;
                                    cycle.Add(currentCycleVertex);
                                    finished = predecessorCounts[predecessor] == -1;
                                    break;
                                }
                            }
                        }

                        cycle.Reverse();

                        HandleCycle(formatCycle, cycle);
                    }
                }
            }

            return sortedQueue;
        }

        private void HandleCycle(Func<IEnumerable<Tuple<TVertex, TVertex, IEnumerable<TEdge>>>, string> formatCycle, List<TVertex> cycle) {
            if (formatCycle == null) {
                // Throw an exception
                throw CircularDependencyError(
                    string.Join(
                        Environment.NewLine + " -> ",
                        cycle.Select(vertex => vertex.ToString())));
            }

            // Build the cycle message data
            var currentCycleVertex = cycle.First();
            var cycleData = new List<Tuple<TVertex, TVertex, IEnumerable<TEdge>>>();

            foreach (var vertex in cycle.Skip(1)) {
                cycleData.Add(Tuple.Create(currentCycleVertex, vertex, GetEdges(currentCycleVertex, vertex)));
                currentCycleVertex = vertex;
            }

            throw CircularDependencyError(formatCycle(cycleData));
        }

        private static Exception CircularDependencyError(string formatCycle) {
            return new InvalidOperationException(string.Format("Unable to progress because a circular dependency was detected: '{0}'.", formatCycle));
        }

        public override IReadOnlyList<List<TVertex>> BatchingTopologicalSort() {
            return BatchingTopologicalSort(null);
        }

        public virtual IReadOnlyList<List<TVertex>> BatchingTopologicalSort(
            [CanBeNull] Func<IEnumerable<Tuple<TVertex, TVertex, IEnumerable<TEdge>>>, string> formatCycle) {
            var currentRootsQueue = new List<TVertex>();
            var predecessorCounts = new Dictionary<TVertex, int>();

            foreach (var vertex in vertices) {
                foreach (var outgoingNeighbour in GetOutgoingNeighbours(vertex)) {
                    if (predecessorCounts.ContainsKey(outgoingNeighbour)) {
                        predecessorCounts[outgoingNeighbour]++;
                    } else {
                        predecessorCounts[outgoingNeighbour] = 1;
                    }
                }
            }

            foreach (var vertex in vertices) {
                if (!predecessorCounts.ContainsKey(vertex)) {
                    currentRootsQueue.Add(vertex);
                }
            }

            var result = new List<List<TVertex>>();
            var nextRootsQueue = new List<TVertex>();
            var currentRootIndex = 0;

            while (currentRootIndex < currentRootsQueue.Count) {
                var currentRoot = currentRootsQueue[currentRootIndex];
                currentRootIndex++;

                // Remove edges from current root and add any exposed vertices to the next batch
                foreach (var successor in GetOutgoingNeighbours(currentRoot)) {
                    predecessorCounts[successor]--;
                    if (predecessorCounts[successor] == 0) {
                        nextRootsQueue.Add(successor);
                    }
                }

                // Roll lists over for next batch
                if (currentRootIndex == currentRootsQueue.Count) {
                    result.Add(currentRootsQueue);

                    currentRootsQueue = nextRootsQueue;
                    currentRootIndex = 0;

                    if (currentRootsQueue.Count != 0) {
                        nextRootsQueue = new List<TVertex>();
                    }
                }
            }

            if (result.Sum(b => b.Count) != vertices.Count) {
                // TODO: Support cycle-breaking?

                var currentCycleVertex = vertices.First(
                    v => {
                        int predecessorNumber;
                        if (predecessorCounts.TryGetValue(v, out predecessorNumber)) {
                            return predecessorNumber != 0;
                        }

                        return false;
                    });
                var cyclicWalk = new List<TVertex> { currentCycleVertex };
                var finished = false;
                while (!finished) {
                    foreach (var predecessor in GetIncomingNeighbours(currentCycleVertex)) {
                        int predecessorCount;
                        if (!predecessorCounts.TryGetValue(predecessor, out predecessorCount)) {
                            continue;
                        }

                        if (predecessorCount != 0) {
                            predecessorCounts[currentCycleVertex] = -1;

                            currentCycleVertex = predecessor;
                            cyclicWalk.Add(currentCycleVertex);
                            finished = predecessorCounts[predecessor] == -1;
                            break;
                        }
                    }
                }

                cyclicWalk.Reverse();

                var cycle = new List<TVertex>();
                var startingVertex = cyclicWalk.First();
                cycle.Add(startingVertex);
                foreach (var vertex in cyclicWalk.Skip(1)) {
                    if (!vertex.Equals(startingVertex)) {
                        cycle.Add(vertex);
                    } else {
                        break;
                    }
                }

                cycle.Add(startingVertex);

                HandleCycle(formatCycle, cycle);
            }

            return result;
        }

        public override IEnumerable<TVertex> Vertices {
            get { return vertices; }
        }

        public override IEnumerable<TVertex> GetOutgoingNeighbours(TVertex from) {
            Dictionary<TVertex, List<TEdge>> successorSet;

            return successorMap.TryGetValue(@from, out successorSet)
                ? successorSet.Keys
                : Enumerable.Empty<TVertex>();
        }

        public override IEnumerable<TVertex> GetIncomingNeighbours(TVertex to) {
            return successorMap.Where(kvp => kvp.Value.ContainsKey(to)).Select(kvp => kvp.Key);
        }


    }

}
