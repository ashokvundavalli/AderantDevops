using System;
using Aderant.Build;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class DoubleWriteTests {

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Duplicate_paths_cause_exception() {

            new DoubleWriteCheck(null).CheckForDoubleWrites(new[] {
                new PathSpec("Foo.dll", "Foo.dll"),
                new PathSpec("Foo.dll", "Foo.dll"),
            });
        }
    }
}
