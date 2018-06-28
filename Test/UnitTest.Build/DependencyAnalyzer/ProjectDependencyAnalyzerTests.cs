using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class ProjectDependencyAnalyzerTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void ProcessVisualStudioProjectTest() {
            ProjectDependencyAnalyzer analyzer = new ProjectDependencyAnalyzer(new CSharpProjectLoader(), new TextTemplateAnalyzer(new PhysicalFileSystem(TestContext.DeploymentDirectory)), new PhysicalFileSystem(TestContext.DeploymentDirectory));

            VisualStudioProject visualStudioProject = new VisualStudioProject(null, Guid.Empty, "Test", null, null) { SolutionRoot = Path.Combine(TestContext.DeploymentDirectory, "Test") };
            IDependencyRef directoryNode = new DirectoryNode("Test", false);

            List<ModuleRef> moduleRefs = new List<ModuleRef> {
                new ModuleRef(new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test1" }, new DependencyManifest("Test", new XDocument()))),
                new ModuleRef(new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test2" }, new DependencyManifest("Test", new XDocument()))),
                new ModuleRef(new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test3" }, new DependencyManifest("Test", new XDocument())))
            };

            moduleRefs[1].DependsOn.Add(moduleRefs[0]);
            moduleRefs[1].DependsOn.Add(moduleRefs[2]);
            moduleRefs[2].DependsOn.Add(moduleRefs[0]);

            var graph = new List<IDependencyRef>();

            graph.Add(new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test" }, new DependencyManifest("Test", new XDocument())));

            foreach (ModuleRef moduleRef in moduleRefs) {
                visualStudioProject.AddDependency(moduleRef);
            }

            graph = analyzer.ProcessVisualStudioProject(visualStudioProject, graph, graph.OfType<VisualStudioProject>().ToList());
            graph.Add(directoryNode);
            graph.Add(visualStudioProject);

            IEnumerable<IDependencyRef> queue = TopologicalSort.Sort(graph, dep => dep.DependsOn);

            Assert.AreEqual(queue.ToArray()[5], visualStudioProject);
        }

        [TestMethod]
        public void ProcessExpertModuleTest() {
            ProjectDependencyAnalyzer analyzer = new ProjectDependencyAnalyzer(new CSharpProjectLoader(), new TextTemplateAnalyzer(new PhysicalFileSystem(TestContext.DeploymentDirectory)), new PhysicalFileSystem(TestContext.DeploymentDirectory));
            List<ExpertModule> expertModules = new List<ExpertModule> {
                new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test1" }, new DependencyManifest("Test", new XDocument())),
                new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test2" }, new DependencyManifest("Test", new XDocument())),
                new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test3" }, new DependencyManifest("Test", new XDocument()))
            };

            expertModules[1].DependsOn.Add(expertModules[0]);
            expertModules[1].DependsOn.Add(expertModules[2]);
            expertModules[2].DependsOn.Add(expertModules[0]);

            var graph = new List<IDependencyRef>();
            foreach (ExpertModule module in expertModules) {
                graph.Add(module);
            }

            foreach (ExpertModule module in expertModules) {
                graph = analyzer.ProcessExpertModule(module, graph);
            }

            var queue = TopologicalSort.Sort(graph, dep => dep.DependsOn).ToList();

            Assert.AreEqual(queue[0], expertModules[0]);
            Assert.AreEqual(queue[2], expertModules[2]);
            Assert.AreEqual(queue[1], expertModules[1]);
        }
    }

}
