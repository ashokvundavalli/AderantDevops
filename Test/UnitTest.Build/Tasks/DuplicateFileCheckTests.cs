using System;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class DuplicateFileCheckTests {

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Duplicate_paths_cause_exception() {

            DuplicateFileCheck.CheckForDoubleWrites(new[] { "Foo.dll", "Foo.dll" });
        }
    }
}
