using System.IO;
using System.Xml;
using Aderant.Build.Tasks;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {

    [TestClass]
    public class ProjectConformityControllerTests {

        [TestMethod]
        public void Project_file_is_modified() {
            using (var reader = XmlReader.Create(new StringReader(Resources.CSharpProject))) {
                var projectRootElement = ProjectRootElement.Create(reader);

                var controller = new ProjectConformityController();
                controller.AddDirProjectIfNecessary(projectRootElement, null);

                Assert.AreEqual(Resources.CSharpProjectWithCommonBuildProjectExpectedResult, projectRootElement.RawXml);
            }
        }

        [TestMethod]
        public void ProjectFileDoesNotAcquireDuplicateCommonBuildProjectImport() {
            using (var reader = XmlReader.Create(new StringReader(Resources.CSharpProjectWithCommonBuildProjectImport))) {
                var projectRootElement = ProjectRootElement.Create(reader);

                var controller = new ProjectConformityController();
                controller.AddDirProjectIfNecessary(projectRootElement, null);

                Assert.AreEqual(Resources.CSharpProjectWithCommonBuildProjectExpectedResult, projectRootElement.RawXml);
            }
        }

        [TestMethod]
        public void ProjectFileDoesNotAcquireDuplicateCommonBuildProjectProperty() {
            using (var reader = XmlReader.Create(new StringReader(Resources.CSharpProjectWithCommonBuildProjectProperty))) {
                var projectRootElement = ProjectRootElement.Create(reader);

                var controller = new ProjectConformityController();
                controller.AddDirProjectIfNecessary(projectRootElement, null);

                Assert.AreEqual(Resources.CSharpProjectWithCommonBuildProjectExpectedResult, projectRootElement.RawXml);
            }
        }

        [TestMethod]
        public void ProjectFileWithNoCSharpImportIsUnchanged() {
            using (var reader = XmlReader.Create(new StringReader(Resources.ProjectWithNoCSharpImport))) {
                var projectRootElement = ProjectRootElement.Create(reader);

                var controller = new ProjectConformityController();
                controller.AddDirProjectIfNecessary(projectRootElement, null);

                Assert.AreEqual(Resources.ProjectWithNoCSharpImport, projectRootElement.RawXml);
            }
        }
    }
}