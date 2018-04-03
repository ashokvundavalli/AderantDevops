using System;
using System.Xml.Linq;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.Sequencer;
using System.Collections.Generic;
using System.IO;

namespace UnitTest.BuildTime {
    [TestClass]
    public class ProjectDependencyAnalyzerTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DependencyReferenceTypeTest() {
            IDependencyRef expertModule = new ExpertModule(new XElement("ReferencedModule", new XAttribute("Name", "Test")));
            IDependencyRef assemblyRef = new AssemblyRef("Test");
            IDependencyRef directoryNode = new DirectoryNode("Test", false);
            IDependencyRef projectRef = new ProjectRef("Test");
            IDependencyRef visualStudioProject = new VisualStudioProject(null, Guid.Empty, null, null, null);

            Assert.AreEqual(ReferenceType.ExpertModule, expertModule.Type);
            Assert.AreEqual(ReferenceType.AssemblyRef, assemblyRef.Type);
            Assert.AreEqual(ReferenceType.DirectoryNode, directoryNode.Type);
            Assert.AreEqual(ReferenceType.ProjectRef, projectRef.Type);
            Assert.AreEqual(ReferenceType.VisualStudioProject, visualStudioProject.Type);
        }

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

            TopologicalSort<IDependencyRef> graph = new TopologicalSort<IDependencyRef>();

            graph.Edge(new ExpertModule(TestContext.DeploymentDirectory, new[] { "Test" }, new DependencyManifest("Test", new XDocument())));

            foreach (ModuleRef moduleRef in moduleRefs) {
                visualStudioProject.AddDependency(moduleRef);
                graph.Edge(moduleRef);
            }

            graph = analyzer.ProcessVisualStudioProject(visualStudioProject, graph);
            graph.Edge(directoryNode);
            graph.Edge(visualStudioProject);

            Assert.AreEqual(6, graph.Vertices.Count);

            Queue<IDependencyRef> queue;
            graph.Sort(out queue);

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
            TopologicalSort<IDependencyRef> graph = new TopologicalSort<IDependencyRef>();

            foreach (ExpertModule module in expertModules) {
                graph.Edge(module);
            }

            foreach (ExpertModule module in expertModules) {
                graph = analyzer.ProcessExpertModule(module, graph);
            }

            Assert.AreEqual(3, graph.Vertices.Count);

            Queue<IDependencyRef> queue;
            graph.Sort(out queue);

            Assert.AreEqual(queue.Dequeue(), expertModules[0]);
            Assert.AreEqual(queue.Dequeue(), expertModules[2]);
            Assert.AreEqual(queue.Dequeue(), expertModules[1]);
        }
    }
}
