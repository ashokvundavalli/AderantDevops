using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class TopologicalSortTests {

        [TestMethod]
        public void TopologicalSort() {
            CollectionAssert.IsSubsetOf(
                MakeThingList(Create("A")), new[] { "A" });

            CollectionAssert.IsSubsetOf(MakeThingList(
                Create("A"),
                Create("B")), new[] { "AB", "BA" });

            CollectionAssert.IsSubsetOf(MakeThingList(
                Create("C", "A", "B"),
                Create("B", "A"),
                Create("A")), new [] { "ABC" });

            CollectionAssert.IsSubsetOf(MakeThingList(
                Create("B", "A"),
                Create("A"),
                Create("C", "A"),
                Create("D", "C", "B")), new[] { "ABCD", "ACBD" });
    }

        public static ThingToSort Create(string entry, params string[] dependencies) {
            return new ThingToSort(entry, dependencies);
        }

        public string[] MakeThingList(params ThingToSort[] expression) {
            var result = Aderant.Build.Utilities.TopologicalSort.Sort(expression, sort => sort.Dependencies);
            return new[] { string.Join("", result.Select(s => s.ToString()).ToArray()) };
        }

        public class ThingToSort : IEquatable<ThingToSort> {
            public readonly ThingToSort[] Dependencies;

            public readonly string Value;

            public ThingToSort(string value, string[] dependencies) {
                if (dependencies != null) {
                    Dependencies = dependencies.Select(s => new ThingToSort(s, null)).ToArray();
                }

                Value = value;
            }

            public override string ToString() {
                return Value;
            }

            public bool Equals(ThingToSort other) {
                return string.Equals(Value, other.Value);
            }

            public override bool Equals(object obj) {
                return Equals((ThingToSort)obj);
            }

            public override int GetHashCode() {
                return (Value != null ? Value.GetHashCode() : 0);
            }
        }
    }

  
}
