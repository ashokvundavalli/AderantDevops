using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace IntegrationTest.Build.DependencyAnalyzer {
    [TestClass]
    [DeploymentItem("DependencyAnalyzer\\Resources\\", "Resources")]
    public class ProjectDependencyAnalyzerTests {
        private DependencyGraph graph;

        private string rootDirectory;
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize() {
            rootDirectory = Path.Combine(TestContext.DeploymentDirectory, @"Resources\Source");
            PhysicalFileSystem fileSystem = new PhysicalFileSystem(rootDirectory);

            var projectDependencyAnalyzer = new ProjectDependencyAnalyzer(
                new CSharpProjectLoader(),
                new TextTemplateAnalyzer(fileSystem),
                fileSystem);

            graph = projectDependencyAnalyzer.GetDependencyGraph(new AnalyzerContext().AddDirectory(rootDirectory));
        }

        [TestMethod]
        public void GetDependencyOrderTest() {
            List<VisualStudioProject> dependencyRefs = graph.GetDependencyOrder().OfType<VisualStudioProject>().ToList();

            var a_a = dependencyRefs[0];
            Assert.AreEqual("ProjectA", a_a.Name);
            Assert.AreEqual("ModuleA", a_a.SolutionDirectoryName);

            var a_c = dependencyRefs[1];
            Assert.AreEqual("ProjectC", a_c.Name);
            Assert.IsTrue(a_c.DependsOn.Contains(new ProjectRef(new Guid("b807f57f-c8df-4129-9f0a-01b7f7aa2ef0"), "ProjectA")));
            Assert.AreEqual("ModuleA", a_c.SolutionDirectoryName);

            var b_a = dependencyRefs[2];
            Assert.AreEqual("ProjectA", b_a.Name);
            Assert.IsTrue(b_a.DependsOn.Contains(new AssemblyRef("ProjectA")));
            Assert.AreEqual("ModuleB", b_a.SolutionDirectoryName);

            var a_b = dependencyRefs[3];
            Assert.AreEqual("ProjectB", a_b.Name);
            Assert.IsTrue(a_b.DependsOn.Contains(new ProjectRef(new Guid("b807f57f-c8df-4129-9f0a-01b7f7aa2ef0"), "ProjectA")));
            Assert.IsTrue(a_b.DependsOn.Contains(new ProjectRef(new Guid("709972C2-4A20-4355-BA9E-F4116AAA8769"), "ProjectC")));
            Assert.AreEqual("ModuleA", a_b.SolutionDirectoryName);

            var b_b = dependencyRefs[4];
            Assert.AreEqual("ProjectB", b_b.Name);
            Assert.IsTrue(b_b.DependsOn.Contains(new AssemblyRef("ProjectA")));
            Assert.IsTrue(b_b.DependsOn.Contains(new AssemblyRef("ProjectB")));
            Assert.AreEqual("ModuleB", b_b.SolutionDirectoryName);
        }

        [TestMethod]
        public void Depending_grouping_test() {
            List<List<IDependencyRef>> groups = graph.GetBuildGroups(graph.GetDependencyOrder());

            Assert.AreEqual(6, groups.Count);
            Assert.AreEqual(1, groups.Last().Count);
        }

        [TestMethod]
        public void Parallel() {
            var sequencer = new BuildSequencer(
                new FakeLogger(),
                new Context(),
                new SolutionFileParser(),
                new PhysicalFileSystem(rootDirectory),
                new Mock<IVersionControlService>().Object);

            var project = sequencer.CreateProject(
                rootDirectory,
                new BuildJobFiles {
                    BeforeProjectFile = "A",
                    AfterProjectFile = "B",
                    JobRunFile = "Z"
                },
                null,
                ComboBuildType.All,
                ProjectRelationshipProcessing.None,
                ProjectBuildConfiguration.DebugAnyCpu);

            XElement projectDocument = sequencer.CreateProjectDocument(project);

            BuildSequencer.SaveBuildProject(@"C:\temp\test.proj", projectDocument);
            Process.Start("notepad++", "\"" + @"C:\temp\test.proj" + "\"");
        }
    }

}
