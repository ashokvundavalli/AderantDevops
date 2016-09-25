using System;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class VersionAnalyzerTests {
        [TestMethod]
        public void When_version_is_zero_a_default_is_returned() {
            var fileVersionAnalyzer = new Mock<FileVersionAnalyzer>();
            fileVersionAnalyzer.Setup(s => s.GetVersion(It.IsAny<string>())).Returns(new FileVersionDescriptor("0.0", "0.0"));

            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.GetFiles(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(new[] { "Foo" });

            var analyzer = new VersionAnalyzer(new FakeLogger(), fs.Object);
            analyzer.Analyzer = fileVersionAnalyzer.Object;

            var version = analyzer.Execute("MyDir");

            Assert.AreEqual(new Version(1, 0, 0), version);
        }

        [TestMethod]
        public void When_no_files_are_versioned_a_default_is_returned() {
            var fileVersionAnalyzer = new Mock<FileVersionAnalyzer>();
            var fs = new Mock<IFileSystem2>();
          
            var analyzer = new VersionAnalyzer(new FakeLogger(), fs.Object);
            analyzer.Analyzer = fileVersionAnalyzer.Object;

            var version = analyzer.Execute("MyDir");

            Assert.AreEqual(new Version(1, 0, 0), version);
        }
    }
}