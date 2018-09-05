using System;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class DoubleWriteTests {

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Duplicate_paths_cause_exception() {

            DoubleWriteCheck.CheckForDoubleWrites(new[] { "Foo.dll", "Foo.dll" });
        }
    }
}
