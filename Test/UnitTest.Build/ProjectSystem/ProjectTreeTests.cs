using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aderant.Build;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using UnitTest.Build.DependencyAnalyzer;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class ProjectTreeTests {

        [TestMethod]
        [ExpectedException(typeof(DuplicateGuidException))]
        public async Task Duplicate_guids_throws_exception() {
            var fsMock = new Mock<IFileSystem2>();
            var services = new ProjectServices { FileSystem = fsMock.Object };

            var solutionManager = new Mock<ISolutionManager>();
            solutionManager.Setup(s => s.GetSolutionForProject(It.IsAny<string>(), It.IsAny<Guid>())).Returns(
                new SolutionSearchResult(null, null) {
                    Found = true,
                    Project = new ProjectInSolutionWrapper(null) {
                        ProjectConfigurations = new Dictionary<string, ProjectConfigurationInSolutionWrapper> { { "Debug|Any CPU", new ProjectConfigurationInSolutionWrapper(includeInBuild:true) } }
                    }
                });

            Guid projectGuid = Guid.NewGuid();
            var tree = new ProjectTree(
                new[] {
                    new TestUnconfiguredProject(projectGuid), new TestUnconfiguredProject(projectGuid)
                });
            tree.Services = services;
            tree.SolutionManager = solutionManager.Object;

            await tree.CollectBuildDependencies(new BuildDependenciesCollector() { ProjectConfiguration = ConfigurationToBuild.Default});
        }
    }

    internal class TestUnconfiguredProject : UnconfiguredProject {
        private readonly Guid guid;

        public TestUnconfiguredProject(Guid guid) {
            this.guid = guid;
        }

        public override ConfiguredProject LoadConfiguredProject(IProjectTree projectTree) {
            return new TestConfiguredProject(projectTree, guid);
        }
    }

}
