using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class ModuleRefTests {
        [TestMethod]
        public void Name_property_returns_module_name() {
            ModuleRef node = new ModuleRef(new ExpertModule("A"));

            Assert.AreEqual("A", (node).Name);
        }

        [TestMethod]
        public void Equality() {
            ModuleRef node1 = new ModuleRef(new ExpertModule("A"));
            ModuleRef node2 = new ModuleRef(new ExpertModule("A"));

            Assert.AreEqual(node1, node2);
            Assert.AreNotSame(node1, node2);
        }
    }
}
