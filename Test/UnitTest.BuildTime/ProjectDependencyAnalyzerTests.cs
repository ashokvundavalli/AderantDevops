using System;
using System.Xml.Linq;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.Sequencer;

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
            ExpertModule expertModule = new ExpertModule { Name = "Test" };
            expertModule.DependsOn.Add(new ExpertModule { Name = "Test" });
            TopologicalSort<IDependencyRef> graph = new TopologicalSort<IDependencyRef>();
            graph.Vertices.Add(expertModule);

            graph = analyzer.ProcessExpertModule(expertModule, graph);
        }
    }
}
