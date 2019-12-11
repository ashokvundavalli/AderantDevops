using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Utilities {
    internal static class Graph {

        /// <summary>
        /// Calls <paramref name="getEdges" /> recursively on all <paramref name="vertices" />
        /// </summary>
        /// <returns>
        /// A sorted graph.
        /// </returns>
        public static IReadOnlyList<TVertex> TopologicalSort<TVertex>(IEnumerable<TVertex> vertices, Func<TVertex, IEnumerable<TVertex>> getEdges) {
            return BuildGraph(vertices, getEdges).TopologicalSort();
        }

        public static IReadOnlyList<TVertex> TopologicalSort<TInput, TVertex>(IEnumerable<TInput> input, Func<TInput, TVertex> getVertex, Func<TInput, IEnumerable<TVertex>> getEdges) {
            Dictionary<TVertex, List<TVertex>> map = new Dictionary<TVertex, List<TVertex>>();

            foreach (TInput item in input) {
                TVertex vertex = getVertex(item);

                map[vertex] = getEdges(item).ToList();
            }

            return BuildGraph(map.Keys, vertex => {
                List<TVertex> edges;
                if (map.TryGetValue(vertex, out edges)) {
                    return edges;
                }

                return Enumerable.Empty<TVertex>();
            }).TopologicalSort();
        }

        public static IReadOnlyList<List<TVertex>> BatchingTopologicalSort<TVertex>(IEnumerable<TVertex> vertices, Func<TVertex, IEnumerable<TVertex>> getEdges) {
            return BuildGraph(vertices, getEdges).BatchingTopologicalSort();
        }

        private static Multigraph<TVertex, int> BuildGraph<TVertex>(IEnumerable<TVertex> vertices, Func<TVertex, IEnumerable<TVertex>> getEdges) {
            var graph = new Multigraph<TVertex, int>();
            Traverse(vertices, getEdges, graph);
            return graph;
        }

        private static void Traverse<TVertex>(IEnumerable<TVertex> vertices, Func<TVertex, IEnumerable<TVertex>> getEdges, Multigraph<TVertex, int> graph) {
            foreach (TVertex vertex in vertices) {

                if (graph.AddVertex(vertex)) {
                    AddEdges(getEdges, graph, getEdges(vertex), vertex);
                }
            }
        }

        private static void AddEdges<TVertex>(Func<TVertex, IEnumerable<TVertex>> getEdges, Multigraph<TVertex, int> graph, IEnumerable<TVertex> edges, TVertex vertex) {
            if (edges != null) {
                foreach (var edge in edges) {
                    bool added = graph.AddVertex(edge);
                    graph.AddEdge(edge, vertex, 0);

                    if (added) {
                        AddEdges(getEdges, graph, getEdges(edge), edge);
                    }
                }
            }
        }
    }
}