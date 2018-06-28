using System.Collections.Generic;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class BuildJobProjectTests {

        [TestMethod]
        public void A_single_project_generates_one_build_group() {
            var mock = new Mock<IFileSystem2>();
            mock.Setup(s => s.Root).Returns("");

            var project = new BuildJob(mock.Object);

            var items = new List<List<IDependencyRef>> {
                new List<IDependencyRef> {
                    new FakeVisualStudioProject()
                }
            };

            Project generateProject = project.GenerateProject(items, new BuildJobFiles { BeforeProjectFile = "A", AfterProjectFile = "B" }, null);

            var targets = generateProject.Elements.OfType<Target>().ToList();
            Assert.AreEqual("RunProjectsToBuild0", targets[0].Name);
            Assert.AreEqual("AfterCompile", targets[1].Name);

        }
    }
}
