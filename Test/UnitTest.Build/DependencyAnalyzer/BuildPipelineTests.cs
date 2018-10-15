using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class BuildPipelineTests {

        [TestMethod]
        public void A_single_project_generates_one_build_group() {
            var mock = new Mock<IFileSystem2>();
            mock.Setup(s => s.Root).Returns("");

            var project = new PipelineProjectBuilder(mock.Object);

            var items = new List<List<IDependable>> {
                new List<IDependable> {
                    new FakeVisualStudioProject()
                }
            };

            Project generateProject = project.GenerateProject(items, new OrchestrationFiles { BeforeProjectFile = "A", AfterProjectFile = "B" }, null);

            var targets = generateProject.Elements.OfType<Target>().ToList();
            Assert.AreEqual("RunProjectsToBuild1", targets[0].Name);
            Assert.AreEqual("AfterCompile", targets[1].Name);
        }

        [TestMethod]
        public void SetUseCommonOutputDirectory_groups_by_solution_root() {
            var projects = new ConfiguredProject[] {
                new TestConfiguredProject(null, null) { SolutionFile = "A.sln", OutputPath = @"..\..\Foo\Bar" },
                new TestConfiguredProject(null, null) { SolutionFile = "A.sln", OutputPath = @"..\..\Foo\Bar" },
                new TestConfiguredProject(null, null) { SolutionFile = "A.sln", OutputPath = @"..\..\Foo\Bar2" },
                new TestConfiguredProject(null, null) { SolutionFile = "B.sln", OutputPath = @"..\..\Foo\Baz" },
            };

            PipelineProjectBuilder.SetUseCommonOutputDirectory(projects);

            Assert.IsTrue(projects[0].UseCommonOutputDirectory);
            Assert.IsTrue(projects[1].UseCommonOutputDirectory);

            Assert.IsFalse(projects[2].UseCommonOutputDirectory);
            Assert.IsFalse(projects[3].UseCommonOutputDirectory);
        }

        [TestMethod]
        public void RemovePropertiesRemovesPropertyTest() {
            string[] properties = new string[] {
                "/p:\"ModuleName=Framework\"",
                "/p:\"T4TransformEnabled=true\""
            };

            string[] newProperties = PipelineProjectBuilder.RemoveProperties(properties, new [] { "T4TransformEnabled" });

            Assert.AreEqual(1, newProperties.Length);
            Assert.IsTrue(string.Equals("/p:\"ModuleName=Framework\"", properties[0]));
        }
    }
}
