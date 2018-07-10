using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;

// (c) Gregory Adam 2009

//http://www.brpreiss.com/books/opus4/html/page557.html
/*   and
 * An Introduction to data structures with applications
 *   Jean-Paul Tremblay - Paul G. Sorensen
 *   pages 494-497
 *   
 * Topological sort of a forest of directed acyclic graphs
 * 
 * The algorithm is pretty straight
 * for each node, a list of succesors is built
 * each node contains its indegree  (predecessorCount)
 *
 * (1) create a queue containing the key of every node that has no predecessors
 * (2) while (the queue is not empty)
 *      dequeue()
 *      output the key
 *      remove the key
 *      
 *      for each node in the successorList
 *          decrement its predecessorCount
 *          if the predecessorCount becomes empty, add it to the queue
 * 
 * 
 * (3) if any node left, then there is a least one cycle
 *
 */

// <T> has to implement IEquatable
// <T> cannot be null - since IDictionary is used

/*
 * 	using Sequencer.Base.Graph
 *		var obj = new Graph.TopologicalSort<T>();		
 */

/* Methods
 * 
 * public bool Edge(Node)
 *	returns true or false
 *	
 * public bool Edge(successor, predecessor)
 *	returns true or false
 *	
 *  public bool Sort(out Queue<T> outQueue)
 *  if true
 *		returns the evaluation queue
 *	else
 *		returns a queue with one cycle
 */
namespace Aderant.Build.Utilities {

    
    public class TopologicalSort {

        public static IEnumerable<T> Sort<T>(IEnumerable<T> items, Func<T, IEnumerable<T>> itemsBefore) where T : class {
            // TODO: I think we need to put back IEqualityComparer as a constraint but if we do that
            // we have poor encapsulation of our dependency system (eg all types need to know how to compare to each other which is awkward)
            var sort = new TopologicalSort<T>();

            foreach (T item in items) {
                sort.Edge(item);

                IEnumerable<T> predecessors = itemsBefore(item);

                if (predecessors != null) {
                    foreach (var predecessor in predecessors) {
                        if (predecessor != null) {
                            sort.Edge(item, predecessor);
                        }
                    }
                }
            }

            Queue<T> queue;
            sort.Sort(out queue);

            return queue;
        }

        public static IEnumerable<TElement> Sort<T, TElement>(IEnumerable<T> items, Func<T, IEnumerable<TElement>> itemsBefore, Func<T, TElement> convert) where T : class where TElement : class {
            var sort = new TopologicalSort<TElement>();

            foreach (T item in items) {
                sort.Edge(convert(item));

                IEnumerable<TElement> predecessors = itemsBefore(item);

                if (predecessors != null) {
                    foreach (var predecessor in predecessors) {
                        if (predecessor != null) {
                            sort.Edge(convert(item), predecessor);
                        }
                    }
                }
            }

            Queue<TElement> queue;
            sort.Sort(out queue);

            return queue;
        }
    }

    public sealed class TopologicalSort<T> : TopologicalSort where T : class {      

        private Dictionary<T, NodeInfo> nodes = new Dictionary<T, NodeInfo>();

        //-------------------------------------------------------------------------
        /// <summary>
        /// Adds a node with nodeKey
        /// Does not complain if the node is already present
        /// </summary>
        /// <param name="nodeKey"></param>
        /// <returns>
        ///		true on success
        ///		false if the nodeKey is null
        /// </returns>
        public bool Edge(T nodeKey) {
            if (nodeKey == null) {
                return false;
            }

            if (!nodes.ContainsKey(nodeKey)) {
                nodes.Add(nodeKey, new NodeInfo());
            }

            return true;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// Add an Edge where successor depends on predecessor.
        /// Does not complain if the directed arc is already in
        /// </summary>
        /// <param name="successor"></param>
        /// <param name="predecessor"></param>
        /// <returns>
        ///		true on success
        ///		false if either parameter is null
        ///			or successor equals predecessor
        /// </returns>
        public bool Edge(T successor, T predecessor) {
            // make sure both nodes are there
            if (!Edge(successor)) {
                return false;
            }

            if (!Edge(predecessor)) {
                return false;
            }

            // if successor == predecessor (cycle) fail
            if (successor.Equals(predecessor)) {
                return false;
            }

            var successorsOfPredecessor = nodes[predecessor].Successors;

            // if the Edge is already there, keep silent
            if (!successorsOfPredecessor.Contains(successor)) {
                // add the successor to the predecessor's successors
                successorsOfPredecessor.Add(successor);

                // increment predecessorCount of successor
                nodes[successor].PredecessorCount++;
            }
            return true;

        }
        //-------------------------------------------------------------------------
        public bool Sort(out Queue<T> sortedQueue) {
            sortedQueue = new Queue<T>(); // create, even if it stays empty

            var outputQueue = new Queue<T>(); // with predecessorCount == 0

            // (1) go through all the nodes
            //		if the node's predecessorCount == 0
            //			add it to the outputQueue
            foreach (KeyValuePair<T, NodeInfo> kvp in nodes) {
                if (kvp.Value.PredecessorCount == 0) {
                    outputQueue.Enqueue(kvp.Key);
                }
            }

            // (2) process the output Queue
            //	output the key
            //	delete the key from Nodes
            //	foreach successor
            //		decrement its predecessorCount
            //		if it becomes zero
            //			add it to the output Queue

            T nodeKey;
            NodeInfo nodeInfo;

            while (outputQueue.Count != 0) {
                nodeKey = outputQueue.Dequeue();

                sortedQueue.Enqueue(nodeKey); // add it to sortedQueue

                nodeInfo = nodes[nodeKey]; // get successors of nodeKey

                nodes.Remove(nodeKey);	// remove it from Nodes

                foreach (T successor in nodeInfo.Successors) {
                    if (--nodes[successor].PredecessorCount == 0) {
                        outputQueue.Enqueue(successor);
                    }
                }

                nodeInfo.Clear();

            }

            // outputQueue is empty here
            if (nodes.Count == 0) {
                return true;   // if there are no nodes left in Nodes, return true
            }

            // there is at least one cycle
            CycleInfo(sortedQueue); // get one cycle in sortedQueue
            return false; // and fail

        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// Clears the Nodes for reuse.  Note that Sort() already does this
        /// </summary>
        public void Clear() {
            foreach (NodeInfo nodeInfo in nodes.Values) {
                nodeInfo.Clear();
            }

            nodes.Clear();
        }
        //-------------------------------------------------------------------------

        /// <summary>
        /// puts one cycle in cycleQueue
        /// </summary>
        /// <param name="cycleQueue"></param>
        public void CycleInfo(Queue<T> cycleQueue) {
            cycleQueue.Clear(); // Clear the queue, it may have data in it

            // init  Cycle info of remaining nodes
            foreach (NodeInfo nodeInfo in nodes.Values) {
                nodeInfo.ContainsCycleKey = nodeInfo.CycleWasOutput = false;
            }

            // (1) put the predecessor in the CycleKey of the successor
            T cycleKey = default(T);
            bool cycleKeyFound = false;

            NodeInfo successorInfo;

            foreach (KeyValuePair<T, NodeInfo> kvp in nodes) {
                foreach (T successor in kvp.Value.Successors) {
                    successorInfo = nodes[successor];

                    if (!successorInfo.ContainsCycleKey) {
                        successorInfo.CycleKey = kvp.Key;
                        successorInfo.ContainsCycleKey = true;

                        if (!cycleKeyFound) {
                            cycleKey = kvp.Key;
                            cycleKeyFound = true;
                        }
                    }
                }
                kvp.Value.Clear();
            }

            if (!cycleKeyFound) {
                throw new Exception("program error: !cycleKeyFound");
            }

            // (2) put a cycle in cycleQueue
            NodeInfo cycleNodeInfo;
            while (!(cycleNodeInfo = nodes[cycleKey]).CycleWasOutput) {
                if (!cycleNodeInfo.ContainsCycleKey) {
                    throw new Exception("program error: nodeInfo.ContainsCycleKey");
                }

                cycleQueue.Enqueue(cycleKey);
                cycleNodeInfo.CycleWasOutput = true;
                cycleKey = cycleNodeInfo.CycleKey;

            }


        }

        private class NodeInfo {
            // for construction
            public int PredecessorCount;
            public List<T> Successors = new List<T>();

            // for Cycles in case the sort fails
            public T CycleKey;
            public bool ContainsCycleKey;
            public bool CycleWasOutput;

            // Clear NodeInfo
            public void Clear() {
                Successors.Clear();
            }

        }
    }
}
