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

        [TestMethod]
        public void GetParentDirectoryTest() {
            IFileSystem2 fileSystem = new PhysicalFileSystem(@"C:\B\737\1\s\Framework\");

            string parentDirectory = fileSystem.GetParent(fileSystem.Root);

            Assert.AreEqual(@"C:\B\737\1\s", parentDirectory);
        }
    }
}