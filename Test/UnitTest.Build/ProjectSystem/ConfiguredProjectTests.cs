using System;
using System.Collections.Generic;
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
            project1.RequireSynchronizedOutputPathsByConfiguration = true;

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
            project1.RequireSynchronizedOutputPathsByConfiguration = true;

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
            project1.RequireSynchronizedOutputPathsByConfiguration = true;

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
        public void Patch_content_can_contain_xaml_items() {
            var tree = new Mock<IProjectTree>();

            var project = new ConfiguredProject(tree.Object);
            project.RequireSynchronizedOutputPathsByConfiguration = true;

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
    }
}