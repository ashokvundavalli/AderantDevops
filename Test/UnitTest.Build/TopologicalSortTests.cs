using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class TopologicalSortTests {

        [TestMethod]
        public void No_cycles_throws_no_exceptions() {
            string[][] successors = new string[][] {
                new[] { "A" },
                new[] { "B" },

            };

            var sorted = Graph.TopologicalSort(new[] { successors[0], successors[1] }, x => Enumerable.Empty<string[]>());
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Cycles_throws_exception() {
            var input =
                new[] {
                    "B".DependsOn("A"),
                    "A".DependsOn("B"),
                };

            var sorted = Graph.TopologicalSort(input,
                tuple => tuple.Item1,
                tuple => tuple.Item2);
        }


        [TestMethod]
        public void Nodes_are_sorted() {
            string[][] successors = new string[][] {
                /* 0 */ new string[] { }, // 0 has no successors
                /* 1 */ new string[] { },
                /* 2 */ new string[] { "3" },
                /* 3 */ new string[] { "1" },
                /* 4 */ new string[] { "0", "1" },
                /* 5 */ new string[] { "0", "2" },
            };

            var sorted = Graph.TopologicalSort(new[] { "4", "5" }, i => successors[int.Parse(i)]);

            Assert.AreEqual(6, sorted.Count);
            AssertSort(new[] { "0", "1", "3", "2", "5", "4" }, sorted.ToArray());
        }

        [TestMethod]
        public void Grouping() {
            var input =
                new[] {
                    "D".DependsOn("A", "B", "C"),
                    "C".DependsOn("A"),
                    "B".DependsOn("A"),
                };

            var multigraph = new Multigraph<string, int>();
            foreach (var node in input) {
                multigraph.AddVertex(node.Item1);

                foreach (var child in node.Item2) {
                    multigraph.AddVertex(child);
                    multigraph.AddEdge(node.Item1, child, 0);
                }
            }

            var batchingTopologicalSort = multigraph.BatchingTopologicalSort();

            Assert.AreEqual(3, batchingTopologicalSort.Count);
        }

        private void AssertSort(string[] expected, params string[] actual) {
            var sequence = string.Join("", actual);

            foreach (var seq in expected) {
                if (Enumerable.SequenceEqual(seq, seq)) {
                    return;
                }
            }

            Assert.Fail($"{sequence} is not an expected sequence");
        }
    }

    internal static class StringExtensions {
        public static Tuple<string, string[]> DependsOn(this string left, params string[] right) {
            return Tuple.Create(left, right);
        }
    }
}