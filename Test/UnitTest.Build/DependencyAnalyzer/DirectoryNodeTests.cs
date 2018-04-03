using System;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class DirectoryNodeTests {

        [TestMethod]
        public void Node_name_Initialize() {
            var node = new DirectoryNode("A", false);

            Assert.AreEqual("A.Initialize", node.Name);

        }

        [TestMethod]
        public void Node_name_Completion() {
            var node = new DirectoryNode("A", true);

            Assert.AreEqual("A.Completion", node.Name);
        }

        [TestMethod]
        public void Node_equality() {
            var node1 = new DirectoryNode("A", true);
            var node2 = new DirectoryNode("A", true);

            Assert.AreEqual(node1, node2);
            Assert.AreNotSame(node1, node2);
        }
    }

    [TestClass]
    public class ModuleRefTests {

        [TestMethod]
        public void Name_property_returns_module_name() {
            var node = new ModuleRef(new ExpertModule("A"));

            Assert.AreEqual("A", node.Name);
        }

        [TestMethod]
        public void Equality() {
            var node1 = new ModuleRef(new ExpertModule("A"));
            var node2 = new ModuleRef(new ExpertModule("A"));

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
            var project = new VisualStudioProject(null, Guid.Empty, null, null, null);

            project.DependsOn.Add(new ModuleRef(new ExpertModule("A")));
            project.DependsOn.Add(new ModuleRef(new ExpertModule("A")));

            Assert.AreEqual(1, project.DependsOn.Count);
        }
    }

}
