using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aderant.Build.Tasks;

namespace UnitTest.Build {
    [TestClass]
    public class FileUtilitiesTests {
        /// <summary>
        /// Exercises FileUtilities.HasExtension
        /// </summary>
        [TestMethod]
        public void HasExtension() {
            Assert.AreEqual("test 1", FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".TXT" }));
            Assert.AreEqual("test 2", !FileUtilities.HasExtension("foo.txt", new string[] { ".EXE", ".DLL" }));
        }
    }
}
