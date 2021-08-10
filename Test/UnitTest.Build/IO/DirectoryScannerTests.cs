using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.IO;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.IO {
    [TestClass]
    public class DirectoryScannerTests {

        [TestMethod]
        public void DirectoryExists_not_called_when_IgnoreMissingDirectories_is_false() {
            var fs = new Moq.Mock<IFileSystem>();

            fs.Setup(s => s.GetDirectories(@"C:\temp", false))
                .Returns(new[] {
                    @"C:\temp\1"
                });

            fs.Setup(s => s.DirectoryExists(@"C:\temp\1")).Verifiable();

            var scanner = new DirectoryScanner(fs.Object, NullLogger.Default);

            scanner.TraverseDirectoriesAndFindFiles(@"C:\temp", new[] { @"myfolder\somefile.file" }, 1);

            fs.Verify(s => s.DirectoryExists(@"C:\temp\1"), Times.Never);
        }

        [TestMethod]
        public void DirectoryExists__called_when_IgnoreMissingDirectories_is_true() {
            var fs = new Moq.Mock<IFileSystem>();

            fs.Setup(s => s.GetDirectories(@"C:\temp", false))
                .Returns(new[] {
                    @"C:\temp\1"
                });

            fs.Setup(s => s.DirectoryExists(@"C:\temp\1")).Verifiable();

            var scanner = new DirectoryScanner(fs.Object, NullLogger.Default);
            scanner.IgnoreMissingDirectories = true;

            scanner.TraverseDirectoriesAndFindFiles(@"C:\temp", new[] { @"myfolder\somefile.file" }, 1);

            fs.Verify(s => s.DirectoryExists(@"C:\temp\1"), Times.Once);
        }

    }
}