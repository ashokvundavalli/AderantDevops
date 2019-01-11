using Aderant.Build;
using Aderant.Build.DependencyResolver.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class PaketDependenciesStructureTests {
        [TestMethod]
        public void InitializerCreatesStringArray() {
            string[] lines = new string[] {
                "source http://packages.ap.aderant.com/packages/nuget",
                "fancy: property",
                "nuget Gotta.Have.It 4.20 ci"
            };
            PaketDependenciesStructure structure = new PaketDependenciesStructure(lines);

            Assert.IsNotNull(structure.Content);
            Assert.AreEqual(3, structure.Content.Length);
        }

        [TestMethod]
        public void GroupGeneration() {
            string[] lines = new string[] {
                "group Test",
                "source test",
                "fancy: property",
                "nuget Gotta.Have.It 4.20 ci"
            };

            int index = 0;
            DependencyGroup group = PaketDependenciesStructure.GenerateGroups(lines, index, out index);

            Assert.IsTrue(group.Name.Equals("Test"));
            Assert.AreEqual(1, group.Sources.Count);
            Assert.AreEqual(1, group.Properties.Count);
            Assert.AreEqual(1, group.Sources.Count);
        }

        [TestMethod]
        public void DatabaseSourceAddition() {
            DependencyGroup group = new DependencyGroup();

            group.AddSource(Constants.PackageServerUrl);

            Assert.AreEqual(1, group.Sources.Count);
        }

        [TestMethod]
        public void SourceRemoval() {
            DependencyGroup group = new DependencyGroup();

            group.AddSource(Constants.PackageServerUrl);
            Assert.AreEqual(1, group.Sources.Count);

            group.RemoveSource(Constants.PackageServerUrl);
            Assert.AreEqual(0, group.Sources.Count);
        }
    }
}
