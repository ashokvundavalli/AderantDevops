using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class DirectoryNodeTests {
        [TestMethod]
        public void Node_name_Initialize() {
            DirectoryNode node = new DirectoryNode("A", null, false);

            Assert.AreEqual("A.Initialize", node.Id);
            Assert.AreEqual("A", node.ModuleName);
            Assert.IsFalse(node.IsPostTargets);

        }

        [TestMethod]
        public void Node_name_Completion() {
            DirectoryNode node = new DirectoryNode("A", null, true);

            Assert.AreEqual("A.Completion", node.Id);
            Assert.AreEqual("A", node.ModuleName);
            Assert.IsTrue(node.IsPostTargets);
        }

        [TestMethod]
        public void Node_equality() {
            DirectoryNode node1 = new DirectoryNode("A", null, true);
            DirectoryNode node2 = new DirectoryNode("A", null, true);

            Assert.AreEqual(node1, node2);
            Assert.AreNotSame(node1, node2);
        }
    }
}
