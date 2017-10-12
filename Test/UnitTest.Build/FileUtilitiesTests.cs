using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class FileUtilitiesTests {
        /// <summary>
        /// Exercises FileUtilities.HasExtension
        /// </summary>
        [TestMethod]
        public void HasExtension() {
            Assert.AreEqual(true, FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".TXT" }));
        }

        /// <summary>
        /// Exercises FileUtilities.HasExtension
        /// </summary>
        [TestMethod]
        public void DoesNotHaveExtension() {
            Assert.AreEqual(false, FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".DLL" }));
        }
    }
}