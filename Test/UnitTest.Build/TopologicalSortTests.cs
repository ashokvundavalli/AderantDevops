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

            var sorted = TopologicalSort.IterativeSort(new[] { successors[0], successors[1] }, x => Enumerable.Empty<string[]>());
        }

        [TestMethod]
        public void Nodes_are_sorted() {
            string[][] successors = new string[][]
            {
                /* 0 */ new string[] { }, // 0 has no successors
                /* 1 */ new string[] { },
                /* 2 */ new string[] { "3" },
                /* 3 */ new string[] { "1" },
                /* 4 */ new string[] { "0", "1" },
                /* 5 */ new string[] { "0", "2" },
            };

            Func<string, IEnumerable<string>> succF = x => successors[int.Parse(x)];
            var sorted = TopologicalSort.IterativeSort(new[] { "4", "5" }, i => succF(i));
            Assert.AreEqual(6, sorted.Count);
            AssertSort(new[] { "0", "1", "3", "2", "5", "4" }, sorted.ToArray());
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
}
