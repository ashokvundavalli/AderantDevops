using System;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class DirectoryNodeFactoryTests {

        [TestMethod]
        public void Initialize_seeds_factory_with_existing_nodes() {
            var d1 = new DirectoryNode("Dir1", @"C:\Dir1", false);

            var graph = new ProjectDependencyGraph(d1);

            var factory = new DirectoryNodeFactory(Mock.Of<IFileSystem>());
            factory.Initialize(graph);

            Tuple<DirectoryNode, DirectoryNode> tuple = factory.Create(graph, "Dir1", "C:\\Dir1");
            Assert.IsNotNull(tuple);
            Assert.IsNotNull(tuple.Item1);
            Assert.IsNotNull(tuple.Item2);
        }
    }
}