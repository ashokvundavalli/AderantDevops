using System.Collections.Generic;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class ProjectDependencyGraphTests {

        [TestMethod]
        public void Reverse_dependency_map() {
            var p1 = new TestConfiguredProject(null);
            p1.outputAssembly = "Abc";

            var p2 = new FakeVisualStudioProject();
            var p3 = new FakeVisualStudioProject();

            p1.AddResolvedDependency(null, p2);
            p1.AddResolvedDependency(null, p3);

            var graph = new ProjectDependencyGraph(p1, p2, p3);

            IReadOnlyCollection<string> projectsThatDirectlyDependOnThisProject = graph.GetProjectsThatDirectlyDependOnThisProject(p3.Id);

            Assert.AreEqual(projectsThatDirectlyDependOnThisProject.First(), p1.Id);
        }
    }
}