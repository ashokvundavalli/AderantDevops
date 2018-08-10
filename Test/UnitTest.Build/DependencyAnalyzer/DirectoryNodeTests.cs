using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class DirectoryNodeTests {
        [TestMethod]
        public void Node_name_pre() {
            DirectoryNode node = new DirectoryNode("A", null, false);

            Assert.AreEqual("A.Pre", node.Id);
            Assert.IsFalse(node.IsPostTargets);

        }

        [TestMethod]
        public void Node_name_post() {
            DirectoryNode node = new DirectoryNode("A", null, true);

            Assert.AreEqual("A.Post", node.Id);
            Assert.IsTrue(node.IsPostTargets);
        }
    }
}
