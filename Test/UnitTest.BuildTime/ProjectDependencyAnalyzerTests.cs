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
            IDependencyRef reference = new AssemblyRef("Test");

            Assert.AreEqual(ReferenceType.AssemblyRef, reference.Type);
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
