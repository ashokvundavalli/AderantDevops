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
            string expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""12.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <CommonBuildProject>$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))</CommonBuildProject>
  </PropertyGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
  <Import Project=""$(CommonBuildProject)\dir.proj"" Condition=""$(CommonBuildProject) != ''"" />
</Project>";

            using (var reader = XmlReader.Create(new StringReader(Resources.CSharpProject))) {
                var projectRootElement = ProjectRootElement.Create(reader);

                var controller = new ProjectConformityController();
                controller.AddDirProjectIfNecessary(projectRootElement, null);

                Assert.AreEqual(expected, projectRootElement.RawXml);
            }
        }
    }
}