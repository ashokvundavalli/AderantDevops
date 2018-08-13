using System.Linq;
using Aderant.Build;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class ProjectTreeTests {

        [TestMethod]
        public void GrovelForFiles_filters_paths() {
            var fsMock = new Mock<IFileSystem2>();
            fsMock.Setup(s => s.GetFiles(It.IsAny<string>(), "*.csproj", true)).Returns(
                new[] {
                    @"Foo\Bar\Baz.csproj",
                    @"Baz\Daz\Maz.csproj",
                });

            var services = new ProjectServices { FileSystem = fsMock.Object };

            var tree = new ProjectTree();
            tree.Services = services;

            var results = tree.GrovelForFiles(
                "",
                new[] {
                    "Bar"
                }).ToList();

            Assert.AreEqual(1, results.Count);
        }

        [TestMethod]
        public void GrovelForFiles_wildcard() {
            var fsMock = new Mock<IFileSystem2>();
            fsMock.Setup(s => s.GetFiles(It.IsAny<string>(), "*.csproj", true)).Returns(
                new[] {
                    @"Foo\Bar\Baz.csproj",
                    @"Baz\Daz\Maz.csproj",
                });

            var services = new ProjectServices { FileSystem = fsMock.Object };

            var tree = new ProjectTree();
            tree.Services = services;

            var results = tree.GrovelForFiles(
                "",
                new[] {
                    "*d*"
                }).ToList();

            Assert.AreEqual(1, results.Count);
        }

        [TestMethod]
        public void GrovelForFiles_FullPath() {
            var fsMock = new Mock<IFileSystem2>();
            fsMock.Setup(s => s.GetFiles(It.IsAny<string>(), "*.csproj", true)).Returns(
                new[] {
                    @"C:\Foo\Bar\Baz.csproj",
                });

            var services = new ProjectServices { FileSystem = fsMock.Object };

            var tree = new ProjectTree();
            tree.Services = services;

            var results = tree.GrovelForFiles(
                "",
                new[] {
                    @"C:\Foo\Bar\..\"
                }).ToList();

            Assert.AreEqual(0, results.Count);
        }
    }
}
