using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class ProjectSequencerTests {

        [TestMethod]
        public async Task MarkDirtyTest() {
            var tree = new Mock<IProjectTree>();

            HashSet<string> dirtyProjects = new HashSet<string> { "ASS1" };

            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "ASS1",
                IsDirty = true,
                IncludeInBuild = true
            };

            var p2 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "ASS2",
                IsDirty = false,
                IncludeInBuild = true
            };

            var m1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "MOD1",
                IncludeInBuild = true
            };

            m1.AddResolvedDependency(null, p1);
            var m2 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "MOD2",

                IncludeInBuild = true
            };
            m2.AddResolvedDependency(null, m1);

            var m3 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "MOD3",
                IncludeInBuild = true
            };
            m3.AddResolvedDependency(null, p2);

            var projectList = new List<ConfiguredProject> { p1, p2, m1, m2, m3 };

            var tree2 = new ProjectTree();
            tree2.Services = new Mock<IProjectServices>().Object;

            projectList.ForEach(tree2.AddConfiguredProject);

            var collector = new BuildDependenciesCollector();
            await tree2.CollectBuildDependencies(collector);

            var sequencer = new ProjectSequencer(NullLogger.Default, null);

            // Mark the projects to dirty directly depends on any project in the search list.
            sequencer.MarkDirty(projectList.OfType<IDependable>().ToList(), dirtyProjects);

            Assert.IsTrue(m1.IsDirty);
            Assert.IsFalse(m2.IsDirty); // This should be unchanged yet.
            Assert.IsFalse(m3.IsDirty);

            // Walk further to all the downstream projects.
            sequencer.MarkDirtyAll(projectList.OfType<IDependable>().ToList(), dirtyProjects);

            Assert.IsTrue(m1.IsDirty);
            Assert.IsTrue(m2.IsDirty); // This is now marked dirty.
            Assert.IsFalse(m3.IsDirty);
        }

        [TestMethod]
        public void ApplyExtensibilityImposition_sets_include_in_build() {
            var tree = new Mock<IProjectTree>();

            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "A",
                IsDirty = false,
                IncludeInBuild = false
            };
            p1.Initialize(null, @"X:\MyProject.proj");

            var sequencer = new ProjectSequencer(NullLogger.Default, null);

            var orchestrationFiles = new OrchestrationFiles() {
                ExtensibilityImposition = new ExtensibilityImposition(
                    new[] {
                        "MyProject.proj"
                    })
            };

            var g = new ProjectDependencyGraph(p1);

            IReadOnlyCollection<IDependable> buildList = sequencer.GetProjectsBuildList(g,
                g.GetDependencyOrder(),
                orchestrationFiles,
                ChangesToConsider.None,
                DependencyRelationshipProcessing.None);

            Assert.AreEqual(1, buildList.Count);
            Assert.IsTrue(p1.IncludeInBuild);
            Assert.IsTrue(p1.IsDirty);
        }

        [TestMethod]
        public void Web_projects_are_always_built() {
            var tree = new Mock<IProjectTree>();

            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "A",
                IsDirty = false,
                IncludeInBuild = false,
                IsWebProject = true,
                SolutionFile = "A\\MySolution.sln",
            };

            var p2 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "A",
                IsDirty = false,
                IncludeInBuild = false,
                IsWebProject = true,
                SolutionFile = "B\\MySolution.sln",
            };

            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            var g = new ProjectDependencyGraph(p1, p2);

            IReadOnlyCollection<IDependable> buildList = sequencer.GetProjectsBuildList(
                g,
                g.GetDependencyOrder(),
                null,
                ChangesToConsider.None,
                DependencyRelationshipProcessing.None);

            Assert.AreEqual(2, buildList.Count);
            Assert.IsTrue(p1.IncludeInBuild);
            Assert.IsTrue(p1.IsDirty);
        }

        [TestMethod]
        public void When_there_are_no_objects_in_the_build_cache_the_project_is_dirty() {
            var tree = new Mock<IProjectTree>();

            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "A",
                IsDirty = false,
                IncludeInBuild = false,
                IsWebProject = true,
                SolutionFile = "MyFile.sln"
            };

            BuildStateFile file = new BuildStateFile();

            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            bool hasLoggedUpToDate = false;
            sequencer.ApplyStateFile(file, "", "", p1, ref hasLoggedUpToDate);

            Assert.AreEqual(p1.BuildReason.Flags, BuildReasonTypes.ProjectOutputNotFound);
            Assert.IsTrue(p1.IsDirty);
        }
    }

}
