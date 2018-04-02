using System;
using System.Xml.Linq;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.Sequencer;
using System.Collections.Generic;

namespace UnitTest.BuildTime {
    [TestClass]
    public class ProjectDependencyAnalyzerTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DependencyReferenceTypeTest() {
            IDependencyRef expertModule = new ExpertModule(new XElement("ReferencedModule", new XAttribute("Name", "Test")));
            IDependencyRef assemblyRef = new AssemblyRef("Test");
            IDependencyRef directoryNode = new DirectoryNode("Test", false);
            IDependencyRef moduleRef = new ModuleRef(new ExpertModule(new XElement("ReferencedModule", new XAttribute("Name", "Test"))));
            IDependencyRef projectRef = new ProjectRef("Test");
            IDependencyRef visualStudioProject = new VisualStudioProject(null, Guid.Empty, null, null, null);

            Assert.AreEqual(ReferenceType.ExpertModule, expertModule.Type);
            Assert.AreEqual(ReferenceType.AssemblyRef, assemblyRef.Type);
            Assert.AreEqual(ReferenceType.DirectoryNode, directoryNode.Type);
            Assert.AreEqual(ReferenceType.ModuleRef, moduleRef.Type);
            Assert.AreEqual(ReferenceType.ProjectRef, projectRef.Type);
            Assert.AreEqual(ReferenceType.VisualStudioProject, visualStudioProject.Type);
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

            Queue<IDependencyRef> queue;
            graph.Sort(out queue);

            Assert.AreEqual(queue.Dequeue(), expertModules[0]);
            Assert.AreEqual(queue.Dequeue(), expertModules[2]);
            Assert.AreEqual(queue.Dequeue(), expertModules[1]);
        }
    }
}
