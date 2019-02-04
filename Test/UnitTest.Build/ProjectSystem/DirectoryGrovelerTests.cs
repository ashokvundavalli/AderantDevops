using System.Linq;
using Aderant.Build;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class DirectoryGrovelerTests {

        [TestMethod]
        public void GrovelForFiles_accepts_wildcard_filters() {
            var fsMock = new Mock<IFileSystem2>();
            fsMock.Setup(s => s.GetDirectories("", false)).Returns(new[] { "C:\\Foo", "C:\\Baz" });

            fsMock.Setup(s => s.GetFiles("C:\\Foo", "*.csproj", false)).Returns(new[] { "C:\\Foo\\Baz.csproj" });
            fsMock.Setup(s => s.GetFiles("C:\\Baz", "*.csproj", false)).Returns(new[] { "C:\\Baz\\Maz.csproj" });

            var groveler = new DirectoryGroveler(fsMock.Object);
            var results = groveler.GrovelForFiles("", new[] { "*m*" }).ToList();

            Assert.AreEqual(1, results.Count);
        }

        [TestMethod]
        public void GrovelForFiles_filters_paths() {
            var fsMock = new Mock<IFileSystem2>();
            fsMock.Setup(s => s.GetDirectories("", false)).Returns(new[] {"Foo", "Baz"});

            fsMock.Setup(s => s.GetFiles("Foo", "*.csproj", false)).Returns(new[] {"Bar.csproj"});
            fsMock.Setup(s => s.GetFiles("Baz", "*.csproj", false)).Returns(new[] {"Maz.csproj"});

            var groveler = new DirectoryGroveler(fsMock.Object);
            var results = groveler.GrovelForFiles("", new[] { "Baz" }).ToList();

            Assert.AreEqual(1, results.Count);
        }
    }
}