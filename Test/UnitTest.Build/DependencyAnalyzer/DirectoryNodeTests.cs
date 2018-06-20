using System;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class DirectoryNodeTests {
        [TestMethod]
        public void Node_name_Initialize() {
            DirectoryNode node = new DirectoryNode("A", false);

            Assert.AreEqual("A.Initialize", node.Name);
            Assert.AreEqual("A", node.ModuleName);
            Assert.IsFalse(node.IsCompletion);

        }

        [TestMethod]
        public void Node_name_Completion() {
            DirectoryNode node = new DirectoryNode("A", true);

            Assert.AreEqual("A.Completion", node.Name);
            Assert.AreEqual("A", node.ModuleName);
            Assert.IsTrue(node.IsCompletion);
        }

        [TestMethod]
        public void Node_equality() {
            DirectoryNode node1 = new DirectoryNode("A", true);
            DirectoryNode node2 = new DirectoryNode("A", true);

            Assert.AreEqual(node1, node2);
            Assert.AreNotSame(node1, node2);
        }
    }

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

    [TestClass]
    public class ExpertModuleAsDependencyTests {

        [TestMethod]
        public void DependsOn_contains_no_duplicates() {
            var expertModule = new ExpertModule();

            expertModule.DependsOn.Add(new AssemblyRef("System.Core"));
            expertModule.DependsOn.Add(new AssemblyRef("System.Core"));

            Assert.AreEqual(1, expertModule.DependsOn.Count);
        }
    }

    [TestClass]
    public class VisualStudioProjectTests {

        [TestMethod]
        public void DependsOn_contains_no_duplicates_for_DirectoryNode() {
            var project = new VisualStudioProject(null, Guid.Empty, null, null, null);

            project.DependsOn.Add(new DirectoryNode("System.Core", true));
            project.DependsOn.Add(new DirectoryNode("System.Core", false));

            Assert.AreEqual(2, project.DependsOn.Count);
        }

        [TestMethod]
        public void DependsOn_contains_no_duplicates_for_DirectoryNode_when_both_are_initialize_nodes() {
            var project = new VisualStudioProject(null, Guid.Empty, null, null, null);

            project.DependsOn.Add(new DirectoryNode("System.Core", true));
            project.DependsOn.Add(new DirectoryNode("System.Core", true));

            Assert.AreEqual(1, project.DependsOn.Count);
        }

        [TestMethod]
        public void DependsOn_contains_no_duplicates_for_DirectoryNode_when_both_are_not_initialize_nodes() {
            var project = new VisualStudioProject(null, Guid.Empty, null, null, null);

            project.DependsOn.Add(new DirectoryNode("System.Core", false));
            project.DependsOn.Add(new DirectoryNode("System.Core", false));

            Assert.AreEqual(1, project.DependsOn.Count);
        }

        [TestMethod]
        public void DependsOn_contains_no_duplicates_for_AssemblyRef() {
            var project = new VisualStudioProject(null, Guid.Empty, null, null, null);

            project.DependsOn.Add(new AssemblyRef("System.Core"));
            project.DependsOn.Add(new AssemblyRef("System.Core"));

            Assert.AreEqual(1, project.DependsOn.Count);
        }

        [TestMethod]
        public void DependsOn_contains_no_duplicates_for_ModuleRef() {
            VisualStudioProject project = new VisualStudioProject(null, Guid.Empty, null, null, null);

            project.DependsOn.Add(new ExpertModule("A"));
            project.DependsOn.Add(new ExpertModule("A"));

            Assert.AreEqual(1, project.DependsOn.Count);
        }
    }
}
