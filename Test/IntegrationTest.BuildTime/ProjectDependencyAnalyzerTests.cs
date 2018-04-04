using System.Collections.Generic;
using System.IO;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer;
using Aderant.BuildTime.Tasks.Sequencer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.BuildTime {
    [TestClass]
    [DeploymentItem("Source", @"Resources\Source")]
    public class ProjectDependencyAnalyzerTests {
        public TestContext TestContext { get; set; }
        private string sourceDirectory;
        private PhysicalFileSystem fileSystem;

        [TestInitialize]
        public void TestInitialize() {
            sourceDirectory = Path.Combine(TestContext.DeploymentDirectory, @"Resources\Source");
            fileSystem = new PhysicalFileSystem(sourceDirectory);
        }

        [TestMethod]
        public void GetDependencyOrderTest() {
            ProjectDependencyAnalyzer projectDependencyAnalyzer = new ProjectDependencyAnalyzer(new CSharpProjectLoader(), new TextTemplateAnalyzer(fileSystem), fileSystem);

            List<IDependencyRef> dependencyOrder = projectDependencyAnalyzer.GetDependencyOrder(new AnalyzerContext().AddDirectory(sourceDirectory));

            Assert.AreEqual("Module.Initialize", dependencyOrder[0].Name);
            Assert.AreEqual("Project1", dependencyOrder[1].Name);
            Assert.AreEqual("Project3", dependencyOrder[2].Name);
            Assert.AreEqual("Project2", dependencyOrder[3].Name);
            Assert.AreEqual("Module.Completion", dependencyOrder[4].Name);

            // Assert dependency order
            Assert.IsTrue(File.Exists(Path.Combine(sourceDirectory, "DependencyGraph.txt")));
        }
    }
}
