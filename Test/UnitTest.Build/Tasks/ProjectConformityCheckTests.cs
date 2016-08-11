using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {

    [TestClass]
    public class ProjectConformityControllerTests {

        [TestMethod]
        public void Project_file_is_modified() {
            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""12.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <CommonBuildProject>$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))</CommonBuildProject>
  </PropertyGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
  <Import Project=""$(CommonBuildProject)\dir.proj"" Condition=""$(CommonBuildProject) != ''"" />
</Project>";

            var fs = new Moq.Mock<IFileSystem2>();
            var p = ProjectConformityController.CreateProject(XDocument.Parse(Resources.CSharpProject));

            var controller = new ProjectConformityController(fs.Object, p);
            controller.AddDirProjectIfNecessary();

            Assert.AreEqual(expected, p.Xml.RawXml);
        }
    }
}
