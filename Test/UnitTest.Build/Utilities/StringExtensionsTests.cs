using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Aderant.Build.Utilities;

namespace UnitTest.Build.Utilities {
    [TestClass]
    public class StringExtensionsTests {

        [TestMethod]
        public void NewGuidFromPathGeneratesDeterministicGuids() {
            const string path = @"C:\Source\Repository\Src\ProjectName\Project.csproj";

            Guid guid1 = path.NewGuidFromPath();
            Guid guid2 = path.NewGuidFromPath();

            Assert.AreEqual(guid1, guid2, "Generated GUIDs should be the same.");
        }

        [TestMethod]
        public void DifferentCapitalizationProducesSameGuid() {
            const string path1 = @"C:\SOURCE\REPOSITORY\SRC\PROJECTNAME\PROJECT.CSPROJ";
            const string path2 = @"c:\source\repository\src\projectname\project.csproj";

            Guid guid1 = path1.NewGuidFromPath();
            Guid guid2 = path2.NewGuidFromPath();

            Assert.AreEqual(guid1, guid2, "Generated GUIDs should be the same.");
        }

        [TestMethod]
        public void NewGuidFromPathGeneratesDifferentGuidsForDifferentPaths() {
            const string path1 = @"C:\Source\Repository\Src\ProjectName\Project.csproj";
            const string path2 = @"C:\Source\Repository\Src\ProjectName\DifferentProject.csproj";

            Guid guid1 = path1.NewGuidFromPath();
            Guid guid2 = path2.NewGuidFromPath();

            Assert.AreNotEqual(guid1, guid2, "Generated GUIDs should be unique.");
        }
    }
}
