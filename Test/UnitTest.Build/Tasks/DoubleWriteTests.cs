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

        [TestMethod]
        public void Globbing_patterns_are_ignored() {
            new DoubleWriteCheck().CheckForDoubleWrites(new[] {
                new PathSpec(@"C:\B\160\1\s\_as\_artifacts\Applications.ExpertOutlookAddIn\1038146\applications.expertoutlookaddin.default\**\**", "MyFile.ico"),
                new PathSpec("MyFile.ico", "MyFile.ico"),
            });
        }
    }
}
