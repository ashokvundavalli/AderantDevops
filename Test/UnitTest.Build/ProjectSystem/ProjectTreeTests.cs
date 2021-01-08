using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;
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


        [TestMethod]
        public async Task ProjectTree_removes_state_files_for_unreconciled_files() {
            Guid projectGuid = Guid.NewGuid();

            var context = new BuildOperationContext();
            context.SourceTreeMetadata = new SourceTreeMetadata() {
                Changes = new List<SourceChange> {
                    new SourceChange("",
                        "ProjectA/MyFile.cs", FileStatus.Added)
                }
            };
            context.BuildStateMetadata = new BuildStateMetadata() {
                BuildStateFiles = new List<BuildStateFile> {
                    new BuildStateFile() {BucketId = new BucketId("123", "ProjectA", BucketVersion.CurrentTree)}
                }
            };
            context.ConfigurationToBuild = ConfigurationToBuild.Default;

            var pipeline = new Moq.Mock<IBuildPipelineService>();

            var tree = new ProjectTree(new[] {
                new TestUnconfiguredProject(projectGuid, ""),
            });

            tree.SequencerFactory = new ExportFactory<ISequencer>(() => {
                return Tuple.Create(new Mock<ISequencer>().Object, new Action(() => {}));
            });

            var manager = new Mock<ISolutionManager>();
            manager.Setup(s => s.GetSolutionForProject(It.IsAny<string>(), It.IsAny<Guid>())).Returns(new SolutionSearchResult("", null) { Found = false });

            tree.SolutionManager = manager.Object;
            await tree.ComputeBuildPlan(context, new AnalysisContext(), pipeline.Object, new OrchestrationFiles());

            Assert.AreEqual(0, context.BuildStateMetadata.BuildStateFiles.Count);
        }
    }

    internal class TestUnconfiguredProject : UnconfiguredProject {
        private readonly Guid guid;

        public TestUnconfiguredProject(Guid guid, string fullPath = null) {
            this.guid = guid;
            this.FullPath = fullPath;
        }

        public override ConfiguredProject LoadConfiguredProject(IProjectTree projectTree) {
            return new TestConfiguredProject(projectTree, this, guid);
        }
    }

}
