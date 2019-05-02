using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class BuildPlanGeneratorTests {

        [TestMethod]
        public void A_single_project_generates_one_build_group() {
            var mock = new Mock<IFileSystem2>();
            mock.Setup(s => s.Root).Returns("");

            var project = new BuildPlanGenerator(mock.Object);

            var items = new List<List<IDependable>> {
                new List<IDependable> {
                    new FakeVisualStudioProject()
                }
            };

            Project generateProject = project.GenerateProject(items, new OrchestrationFiles { BeforeProjectFile = "A", AfterProjectFile = "B" }, false);

            var targets = generateProject.Elements.OfType<Target>().ToList();
            Assert.AreEqual("RunProjectsToBuild1", targets[0].Name);
            Assert.AreEqual("AfterCompile", targets[1].Name);
        }

        [TestMethod]
        public void AlwaysBuild_flag_adds_rebuild_target() {
            var mock = new Mock<IFileSystem2>();
            mock.Setup(s => s.Root).Returns("");

            var project = new BuildPlanGenerator(mock.Object);

            var configuredProject = new TestConfiguredProject(null) {
                SolutionFile = "A.sln",
                IncludeInBuild = true,
                OutputPath = @"..\..\Foo\Bar",
                IsWebProject = false,
                ProjectTypeGuids = new[] {
                    WellKnownProjectTypeGuids.TestProject
                },
                BuildReason = new BuildReason { Flags = BuildReasonTypes.AlwaysBuild }
            };

            configuredProject.Initialize(null, "some_file.csproj");

            var items = new List<List<IDependable>> {
                new List<IDependable> {
                   configuredProject
                }
            };

            project.ItemGroupItemMaterialized += (sender, args) => {
                var argsItemGroupItem = args.ItemGroupItem;
                Assert.AreEqual("Rebuild", argsItemGroupItem["Targets"]);
            };

            Project generateProject = project.GenerateProject(
                items,
                new OrchestrationFiles
                {
                    BeforeProjectFile = "A",
                    AfterProjectFile = "B"
                },
                false);

            generateProject.CreateXml();
        }

        [TestMethod]
        public void SetUseCommonOutputDirectory_groups_by_solution_root() {
            var projects = new ConfiguredProject[] {
                new TestConfiguredProject(null) { SolutionFile = "A.sln", OutputPath = @"..\..\Foo\Bar" },
                new TestConfiguredProject(null) { SolutionFile = "A.sln", OutputPath = @"..\..\Foo\Bar" },
                new TestConfiguredProject(null) { SolutionFile = "A.sln", OutputPath = @"..\..\Foo\Bar2" },
                new TestConfiguredProject(null) { SolutionFile = "B.sln", OutputPath = @"..\..\Foo\Baz" },
            };

            BuildPlanGenerator.SetUseCommonOutputDirectory(projects);

            Assert.IsTrue(projects[0].UseCommonOutputDirectory);
            Assert.IsTrue(projects[1].UseCommonOutputDirectory);

            Assert.IsFalse(projects[2].UseCommonOutputDirectory);
            Assert.IsFalse(projects[3].UseCommonOutputDirectory);
        }

        [TestMethod]
        public void Properties_contains_solution_directory_path_when_rsp_file_exists() {
            Mock<IFileSystem> mock = new Mock<IFileSystem>();
            mock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(true);
            mock.Setup(s => s.OpenFile(It.IsAny<string>())).Returns(new MemoryStream(Encoding.Default.GetBytes("")));

            var generator = new BuildPlanGenerator(mock.Object);

            var propertyList = new PropertyList();
            PropertyList list = generator.AddBuildProperties(propertyList, mock.Object, "abc");

            Assert.IsTrue(list.ContainsKey("SolutionDirectoryPath"));
            Assert.IsTrue(list.ContainsValue(@"abc\"));
        }

        [TestMethod]
        public void Properties_contains_solution_directory_path_when_rsp_file_does_not_exist() {
            Mock<IFileSystem> mock = new Mock<IFileSystem>();
            mock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(false);

            var generator = new BuildPlanGenerator(mock.Object);

            var propertyList = new PropertyList();
            PropertyList list = generator.AddBuildProperties(propertyList, mock.Object, "abc");

            Assert.IsTrue(list.ContainsKey("SolutionDirectoryPath"));
            Assert.IsTrue(list.ContainsValue(@"abc\"));
        }


        [TestMethod]
        public void Run_user_targets_is_added_to_directory_nodes() {
            var mock = new Mock<IFileSystem2>();
            var project = new BuildPlanGenerator(mock.Object);

            var items = new List<List<IDependable>> {
                new List<IDependable> {
                    new DirectoryNode("Foo", "", false)
                }
            };

            PropertyList list = null;
            project.ItemGroupItemMaterialized += (sender, args) => { list = args.Properties; };

            Project generateProject = project.GenerateProject(
                items,
                new OrchestrationFiles {
                    BeforeProjectFile = "A",
                    AfterProjectFile = "B"
                },
                false);

            generateProject.CreateXml();

            Assert.IsNotNull(list);
            Assert.IsTrue(list.ContainsKey("RunUserTargets"));
            Assert.IsTrue(string.Equals(list["RunUserTargets"], "true", StringComparison.OrdinalIgnoreCase));
        }
    }
}