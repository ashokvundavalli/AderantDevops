using System;
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

            Assert.IsTrue(File.Exists(Path.Combine(sourceDirectory, "DependencyGraph.txt")));
        }
    }
}
