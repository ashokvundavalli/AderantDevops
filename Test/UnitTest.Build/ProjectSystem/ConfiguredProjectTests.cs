using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.VersionControl.Model;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class ConfiguredProjectTests {

        /// <summary>
        /// Ensures that the property memoization does not bleed across project instances.
        /// </summary>
        [TestMethod]
        public void Property_memoization_returns_values_for_project() {
            var tree = new Mock<IProjectTree>();

            var cfg1 = new ConfiguredProject(tree.Object);
            cfg1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup = element.AddPropertyGroup();
                        propertyGroup.AddProperty("ProjectTypeGuids", "{3AC096D0-A1C2-E12C-1390-A8335801FDAB};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
                        propertyGroup.AddProperty("OutputType", "WinExe");
                        propertyGroup.AddProperty("AssemblyName", "Blah");
                        return element;
                    }),
                "");

            var cfg2 = new ConfiguredProject(tree.Object);
            cfg2.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup = element.AddPropertyGroup();
                        propertyGroup.AddProperty("ProjectTypeGuids", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
                        return element;
                    }),
                "");

            Assert.AreEqual("WinExe", cfg1.OutputType);
            Assert.AreEqual("Blah", cfg1.OutputAssembly);

            Assert.AreEqual(2, cfg1.ProjectTypeGuids.Count);
            Assert.AreEqual(1, cfg2.ProjectTypeGuids.Count);

            Assert.AreEqual(2, cfg1.ProjectTypeGuids.Count);
            Assert.AreEqual(1, cfg2.ProjectTypeGuids.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(BuildPlatformException))]
        public void When_paths_are_not_the_same_an_exception_is_thrown() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);
            project1.RequireSynchronizedOutputPaths = true;

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup1 = element.AddPropertyGroup();
                        propertyGroup1.Condition = " '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ";
                        propertyGroup1.AddProperty("OutputPath", @"\..\Bin\Module\");

                        ProjectPropertyGroupElement propertyGroup2 = element.AddPropertyGroup();
                        propertyGroup2.Condition = " '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ";
                        propertyGroup2.AddProperty("OutputPath", @"\..\Bin\Module1\");

                        ProjectPropertyGroupElement propertyGroup3 = element.AddPropertyGroup();
                        propertyGroup3.Condition = " '$(Configuration)|$(Platform)' == 'Debug|x86' ";
                        propertyGroup3.AddProperty("OutputPath", @"\..\Bin\Module\");

                        return element;
                    }),
                "");

            project1.Validate("Debug", "AnyCPU");
        }

        [TestMethod]
        public void When_paths_are_the_same_an_exception_is_not_thrown() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);
            project1.RequireSynchronizedOutputPaths = true;

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup1 = element.AddPropertyGroup();
                        propertyGroup1.Condition = " '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ";
                        propertyGroup1.AddProperty("OutputPath", @"\..\Bin\Module\");

                        ProjectPropertyGroupElement propertyGroup2 = element.AddPropertyGroup();
                        propertyGroup2.Condition = " '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ";
                        propertyGroup2.AddProperty("OutputPath", @"\..\Bin\Module\");

                        return element;
                    }),
                "");

            project1.Validate("Debug", "AnyCPU");
        }

        [TestMethod]
        public void Path_check_uses_path_normalization() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);
            project1.RequireSynchronizedOutputPaths = true;

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup1 = element.AddPropertyGroup();
                        propertyGroup1.Condition = " '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ";
                        propertyGroup1.AddProperty("OutputPath", @"\..\Bin\Module\");

                        ProjectPropertyGroupElement propertyGroup2 = element.AddPropertyGroup();
                        propertyGroup2.Condition = " '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ";
                        propertyGroup2.AddProperty("OutputPath", @"\..\Bin\Module\\\");

                        return element;
                    }),
                "");

            project1.Validate("Debug", "AnyCPU");
        }

        [TestMethod]
        public void When_single_path_defined_then_no_error() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);
            project1.RequireSynchronizedOutputPaths = true;

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var reader = XmlReader.Create(new StringReader(ProjectSystemResources.Project_with_single_output_path));
                        var element = ProjectRootElement.Create(reader);
                        return element;
                    }),
                "");

            project1.Validate("Debug", "AnyCPU");
        }

        [TestMethod]
        [ExpectedException(typeof(BuildPlatformException))]
        public void Platform_target_validation() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);
            project1.RequireSynchronizedOutputPaths = true;

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var reader = XmlReader.Create(new StringReader(ProjectSystemResources.Conflicting_platformtarget));
                        var element = ProjectRootElement.Create(reader);
                        return element;
                    }),
                "");

            project1.Validate("Debug", "AnyCPU");
        }

        [TestMethod]
        public void Platform_target_validation_can_be_disabled() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);
            project1.RequireSynchronizedOutputPaths = false;

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var reader = XmlReader.Create(new StringReader(ProjectSystemResources.Conflicting_platformtarget));
                        var element = ProjectRootElement.Create(reader);
                        return element;
                    }),
                "");

            project1.Validate("Debug", "AnyCPU");
        }

        [TestMethod]
        public void Windows_installer_project_properties() {
            var tree = new Mock<IProjectTree>();

            var project1 = new ConfiguredProject(tree.Object);

            project1.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var reader = XmlReader.Create(new StringReader(ProjectSystemResources.MyWindowsInstallerApp));
                        var element = ProjectRootElement.Create(reader);
                        return element;
                    }),
                "");

            Assert.AreEqual("Package", project1.OutputType);
            Assert.AreEqual("ExpertAssistantPerUser.msi", project1.GetOutputAssemblyWithExtension());
        }

        [TestMethod]
        public void Patch_content_can_contain_xaml_items() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);
            project.RequireSynchronizedOutputPaths = true;

            project.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        element.AddItem("Page", "MyPage.xaml");
                        return element;
                    }),
                "");

            project.CalculateDirtyStateFromChanges(new List<ISourceChange> { new SourceChange("", "MyPage.xaml", FileStatus.Modified) });

            Assert.IsTrue(project.IsDirty);
        }

        [TestMethod]
        public void CheckCleanProject() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);
            project.RequireSynchronizedOutputPaths = true;

            project.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        element.AddItem("Page", "CoolFile.cs");
                        return element;
                    }),
                string.Empty);

            project.CalculateDirtyStateFromChanges(new List<ISourceChange> { new SourceChange(string.Empty, "SomeoneElsesFile.cs", FileStatus.Modified) });

            Assert.IsFalse(project.IsDirty);
        }

        [TestMethod]
        public void CheckMultipleChangeViolation() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);
            project.RequireSynchronizedOutputPaths = true;

            project.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        element.AddItem("Page", "CoolFile.cs");
                        return element;
                    }),
                string.Empty);

            project.CalculateDirtyStateFromChanges(new List<ISourceChange> {
                new SourceChange(string.Empty, "SomeoneElsesFile.cs", FileStatus.Modified),
                new SourceChange(string.Empty, "CoolFile.cs", FileStatus.Modified)
            });

            Assert.IsTrue(project.IsDirty);
        }

        [TestMethod]
        public void WixLib_projects_have_ouput_type_of_wixlib() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);
            project.RequireSynchronizedOutputPaths = true;

            project.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup1 = element.AddPropertyGroup();
                        propertyGroup1.AddProperty("TargetExt", ".wixlib");

                        return element;
                    }),
                "");

            Assert.AreEqual(".wixlib", project.OutputType);
        }

        [TestMethod]
        public void WinExe_projects_have_ouput_type_of_WinExe() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);
            project.RequireSynchronizedOutputPaths = true;

            project.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        var element = ProjectRootElement.Create();
                        ProjectPropertyGroupElement propertyGroup1 = element.AddPropertyGroup();
                        propertyGroup1.AddProperty("TargetExt", "WinExe");

                        return element;
                    }),
                "");

            Assert.AreEqual("WinExe", project.OutputType);
        }

        [TestMethod]
        public void Can_import_with_bad_imports() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);

            project.Initialize(
                new Lazy<ProjectRootElement>(
                    () => {
                        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildThisFileDirectory)\does\not\exist.props"" />
  </Project>";

                        var element = ProjectRootElement.Create(XmlReader.Create(new StringReader(xml)));
                        return element;
                    }),
                "");

            var projectIsTestProject = project.IsTestProject; // Force some kind of evaluation
        }
    }

}