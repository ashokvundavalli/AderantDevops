using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class FileSystemTests {

        [TestMethod]
        public void ComputeSha1Hash() {
            string hash = new PhysicalFileSystem().ComputeSha1Hash(typeof(FileSystem).Assembly.Location);

            Assert.IsNotNull(hash);
        }
    }
}