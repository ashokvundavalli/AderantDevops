using System.Collections.Generic;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Model;
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

            var project = new BuildPipeline(mock.Object);

            var items = new List<List<IDependable>> {
                new List<IDependable> {
                    new FakeVisualStudioProject()
                }
            };

            Project generateProject = project.GenerateProject(items, new OrchestrationFiles { BeforeProjectFile = "A", AfterProjectFile = "B" }, null);

            var targets = generateProject.Elements.OfType<Target>().ToList();
            Assert.AreEqual("RunProjectsToBuild1", targets[0].Name);
            Assert.AreEqual("AfterCompile", targets[1].Name);
        }
    }
}
