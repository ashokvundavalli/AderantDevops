using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.TeamFoundation.Framework.Client.Catalog.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyAnalyzer {
    [TestClass]
    public class ProjectSequencerTests {

        public TestContext TestContext { get; set; }

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
        public void When_a_test_project_is_dirty_any_related_web_projects_are_included_in_build() {
            var p1 = new TestConfiguredProject(null) {
                outputAssembly = "UnitTest1",
                IsDirty = true,
                IncludeInBuild = true,
                IsWebProject = false,
                ProjectTypeGuids = new []{WellKnownProjectTypeGuids.TestProject}
            };

            var p2 = new TestConfiguredProject(null) {
                outputAssembly = "MyWebApp1",
                IsDirty = false,
                IncludeInBuild = true,
                IsWebProject = true,
                ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.TestProject }
            };

            p1.AddResolvedDependency(null, p2);

            var graph = new ProjectDependencyGraph(p1, p2);
            var ctx = new BuildOperationContext {
                BuildRoot = "",
                Switches = new BuildSwitches()
            };

            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            sequencer.CreatePlan(ctx, new OrchestrationFiles(), graph, false);

            Assert.IsTrue(p2.IsDirty, "This project should be included in the build as the test project is being built.");
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

            IReadOnlyCollection<IDependable> buildList = sequencer.GetProjectsBuildList(
                g,
                g.GetDependencyOrder(),
                orchestrationFiles,
                false,
                ChangesToConsider.None,
                DependencyRelationshipProcessing.None);

            Assert.AreEqual(1, buildList.Count);
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

        [TestMethod]
        public void AddedByDependencyAnalysis_is_false_when_user_selects_that_directory_to_build() {
            var sequencer = new ProjectSequencer(new NullLogger(), new Mock<IFileSystem>().Object);

            var ps = new Mock<IBuildPipelineService>();
            // Simulate the "Bar" directory being added by expanding the build tree via
            // analysis
            ps.Setup(s => s.GetContributors()).Returns(
                new[] {
                    new BuildDirectoryContribution("Temp\\Bar\\" + WellKnownPaths.EntryPointFilePath) {
                        DependencyFile = "Temp\\Bar\\Build\\" + DependencyManifest.DependencyManifestFileName,
                        DependencyManifest = new DependencyManifest(
                            "Bar",
                            XDocument.Parse(Resources.DependencyManifest))
                    },
                });

            sequencer.PipelineService = ps.Object;

            List<DirectoryNode> nodes = sequencer.SynthesizeNodesForAllDirectories(
                new[] {
                    @"Temp\Foo\" + WellKnownPaths.EntryPointFilePath,
                    // User also specifies Bar to build, so this nullifies the expansion added by GetContributors
                    @"Temp\Bar\" + WellKnownPaths.EntryPointFilePath,
                },
                new ProjectDependencyGraph());

            Assert.IsTrue(nodes.All(s => s.AddedByDependencyAnalysis == false));
        }

        [TestMethod]
        public void AddedByDependencyAnalysis_is_true_when_user_selects_that_directory_to_build() {
            var sequencer = new ProjectSequencer(new NullLogger(), new Mock<IFileSystem>().Object);

            var ps = new Mock<IBuildPipelineService>();
            // Simulate the "Bar" directory being added by expanding the build tree via
            // analysis
            ps.Setup(s => s.GetContributors()).Returns(
                new[] {
                    new BuildDirectoryContribution("Temp\\Bar\\" + WellKnownPaths.EntryPointFilePath) {
                        DependencyFile = "Temp\\Bar\\Build\\" + DependencyManifest.DependencyManifestFileName,
                        DependencyManifest = new DependencyManifest(
                            "Bar",
                            XDocument.Parse(Resources.DependencyManifest))
                    },
                });

            sequencer.PipelineService = ps.Object;

            List<DirectoryNode> nodes = sequencer.SynthesizeNodesForAllDirectories(
                new[] {
                    @"Temp\Foo\" + WellKnownPaths.EntryPointFilePath,
                },
                new ProjectDependencyGraph());

            Assert.IsNotNull(nodes.SingleOrDefault(s => s.DirectoryName == "Foo" && !s.AddedByDependencyAnalysis));
            Assert.IsNotNull(nodes.SingleOrDefault(s => s.DirectoryName == "Bar" && s.AddedByDependencyAnalysis));
        }

        [TestMethod]
        public void WriteBuildTreeProducesFile() {
            IFileSystem fileSystem = new PhysicalFileSystem();
            BuildOperationContext context = new BuildOperationContext {
                BuildRoot = TestContext.DeploymentDirectory
            };

            ProjectSequencer.WriteBuildTree(fileSystem, context, Resources.BuildTree);

            string output = Path.Combine(TestContext.DeploymentDirectory, "BuildTree.txt");

            Assert.IsTrue(fileSystem.FileExists(output));
            Assert.AreEqual(Resources.BuildTree, fileSystem.ReadAllText(output));
        }
    }
}