using System.Collections.Generic;
using System.IO;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.DependencyAnalyzer {
    [TestClass]
    [DeploymentItem("DependencyAnalyzer\\Resources\\", "Resources")]
    public class ProjectDependencyAnalyzerTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void GetDependencyOrderTest() {
            string sourceDirectory = Path.Combine(TestContext.DeploymentDirectory, @"Resources\Source");
            PhysicalFileSystem fileSystem = new PhysicalFileSystem(sourceDirectory);

            ProjectDependencyAnalyzer projectDependencyAnalyzer = new ProjectDependencyAnalyzer(new CSharpProjectLoader(), new TextTemplateAnalyzer(fileSystem), fileSystem);

            List<IDependencyRef> dependencyOrder = projectDependencyAnalyzer.GetDependencyOrder(new AnalyzerContext().AddDirectory(sourceDirectory));

            Assert.AreEqual("Module.Initialize", dependencyOrder[0].Name);
            Assert.AreEqual("Project1", dependencyOrder[1].Name);
            Assert.AreEqual("Project3", dependencyOrder[2].Name);
            Assert.AreEqual("Project2", dependencyOrder[3].Name);
            Assert.AreEqual("Module.Completion", dependencyOrder[4].Name);
            Assert.IsTrue(File.Exists(Path.Combine(sourceDirectory, "DependencyGraph.txt")));
        }

     
    }

}
