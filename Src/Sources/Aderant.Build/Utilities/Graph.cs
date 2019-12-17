using Aderant.Build.Annotations;
using System.Collections.Generic;

namespace Aderant.Build.Utilities {

    /// <remarks>
    /// Copyright (c) .NET Foundation. All rights reserved.
    /// Licensed under the Apache License, Version 2.0.
    /// </remarks>
    internal abstract class Graph<TVertex> {

        public abstract IEnumerable<TVertex> Vertices { get; }

        public abstract IEnumerable<TVertex> GetOutgoingNeighbours([NotNull] TVertex from);

        public abstract IEnumerable<TVertex> GetIncomingNeighbours([NotNull] TVertex to);

        public abstract IReadOnlyList<TVertex> TopologicalSort();

        public abstract IReadOnlyList<List<TVertex>> BatchingTopologicalSort();

        public virtual ISet<TVertex> GetUnreachableVertices([NotNull] IReadOnlyList<TVertex> roots) {
            var unreachableVertices = new HashSet<TVertex>(Vertices);
            unreachableVertices.ExceptWith(roots);
            var visitingQueue = new List<TVertex>(roots);

            var currentVertexIndex = 0;
            while (currentVertexIndex < visitingQueue.Count) {
                var currentVertex = visitingQueue[currentVertexIndex];
                currentVertexIndex++;
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var neighbour in GetOutgoingNeighbours(currentVertex)) {
                    if (unreachableVertices.Remove(neighbour)) {
                        visitingQueue.Add(neighbour);
                    }
                }
            }

            return unreachableVertices;
        }
    }
}

