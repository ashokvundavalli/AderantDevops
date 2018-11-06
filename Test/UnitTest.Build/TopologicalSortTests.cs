using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class TopologicalSortTests {

        [TestMethod]
        public void TopologicalSort() {

            AssertSort(new FooType[] { "A" }, "A");

            AssertSort(
                new FooType[] {
                    "A",
                    "B",
                },
                "AB",
                "BA");

            AssertSort(
                new[] {
                    ((FooType)"C")["A"]["B"],
                    ((FooType)"B")["A"],
                    "A"
                },
                "ABC");

            AssertSort(
                new[] {
                    ((FooType)"B")["A"],
                    ((FooType)"A"),
                    ((FooType)"C")["A"],
                    ((FooType)"D")["C"]["B"],

                },
                "ABCD",
                "ACBD");
        }

        private void AssertSort(FooType[] fooTypes, params string[] expectedSequence) {
            var result = TopologicalSorter.TopologicalSort(fooTypes, sort => sort.GetDependencies().Select(s =>(FooType)s));

            var sequence = string.Join("", result);

            foreach (var seq in expectedSequence) {
                if (Enumerable.SequenceEqual(seq, seq)) {
                    return;
                }
            }

            Assert.Fail($"{sequence} is not an expected sequence");
        }
    }

    public class FooType {
        private readonly string str;
        private List<string> dependencies = new List<string>();

        private FooType(string str) {
            this.str = str;
        }

        public FooType this[string s] {
            get {
                this.dependencies.Add(s);
                return this;
            }
        }

        public static implicit operator FooType(string str) {
            return new FooType(str);
        }

        public static explicit operator string(FooType v) {
            return v.str;
        }

        public IEnumerable<string> GetDependencies() {
            return dependencies;
        }
    }

}
