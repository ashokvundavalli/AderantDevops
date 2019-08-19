using System;
using System.IO;
using System.Reflection;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class FileSystemTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void ComputeSha1Hash() {
            string hash = new PhysicalFileSystem().ComputeSha1Hash(typeof(FileSystem).Assembly.Location);

            Assert.IsNotNull(hash);
        }

        [TestMethod]
        public void CheckSameFileExistenceTest() {
            string file = Assembly.GetExecutingAssembly().Location;

            // File does exist as the source and target file locations are the same.
            Assert.AreEqual(PhysicalFileSystem.FilesRelationship.DestinationExists, PhysicalFileSystem.CheckFileExistence(file, file));
        }

        [TestMethod]
        public void NoFileExistsTest() {
            string file = Assembly.GetExecutingAssembly().Location;

            Assert.AreEqual(PhysicalFileSystem.FilesRelationship.NonExistent, PhysicalFileSystem.CheckFileExistence(file, Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName())));
        }

        [TestMethod]
        [DeploymentItem(@"Resources\GoodBuildLog.txt")]
        public void CheckJunctionFileExistenceTest() {
            string sourceFile = Path.Combine(TestContext.DeploymentDirectory, "GoodBuildLog.txt");
            string targetFile = string.Concat(sourceFile, Path.GetRandomFileName());

            try {
                // Create Hard Link between source and target files and check if we can validate that they have a junction.
                NativeMethods.CreateHardLink(targetFile, sourceFile, IntPtr.Zero);
                Assert.AreEqual(PhysicalFileSystem.FilesRelationship.Junction, PhysicalFileSystem.CheckFileExistence(sourceFile, targetFile));
            } finally {
                File.Delete(targetFile);
            }
        }
    }
}
