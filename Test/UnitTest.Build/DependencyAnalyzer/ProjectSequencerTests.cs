﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using UnitTest.Build.Helpers;
using UnitTest.Build.StateTracking;
using Task = System.Threading.Tasks.Task;

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
        public void When_a_downstream_project_is_affected_then_it_will_not_use_build_cache() {
            var p1 = new TestConfiguredProject(null) {
                outputAssembly = "UnitTest1",
                IsDirty = false,
                IncludeInBuild = true,
                IsWebProject = false,
                ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.TestProject },
                BuildReason = new BuildReason()
            };

            var p2 = new TestConfiguredProject(null) {
                outputAssembly = "MyWebApp1",
                IsDirty = true,
                IncludeInBuild = true,
                IsWebProject = true,
                ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.WorkflowFoundation },
                BuildReason = new BuildReason()
            };

            p1.AddResolvedDependency(null, p2);

            var ctx = new BuildOperationContext {
                BuildRoot = "",
                Switches = new BuildSwitches {
                    Downstream = true
                },
                BuildMetadata = new BuildMetadata(),
                StateFiles = new List<BuildStateFile> { new BuildStateFile() }
            };

            var graph = new ProjectDependencyGraph(p1, p2);
            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            sequencer.CreatePlan(ctx, new OrchestrationFiles(), graph, false, null);

            Assert.IsNotNull(p2.DirectoryNode.RetrievePrebuilts);
            Assert.IsFalse(p2.DirectoryNode.RetrievePrebuilts.Value);
        }


        [TestMethod]
        public void When_a_test_project_is_dirty_any_related_web_projects_are_included_in_build() {
            var p1 = new TestConfiguredProject(null) {
                outputAssembly = "UnitTest1",
                IsDirty = true,
                IncludeInBuild = true,
                IsWebProject = false,
                ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.TestProject }
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
                Switches = new BuildSwitches(),
                BuildMetadata = new BuildMetadata()
            };

            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            sequencer.CreatePlan(ctx, new OrchestrationFiles(), graph, false, null);

            Assert.IsTrue(p2.IsDirty, "This project should be included in the build as the test project is being built.");
        }

        [TestMethod]
        public void ApplyExtensibilityImposition_sets_include_in_build() {
            var tree = new Mock<IProjectTree>();

            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "A",
                IsDirty = false,
                IncludeInBuild = false,
                IsWebProject = false,
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
            sequencer.ApplyStateFile(new BuildStateFile[] { file }, string.Empty, p1, false, ref hasLoggedUpToDate, null);

            Assert.AreEqual(BuildReasonTypes.ProjectOutputNotFound, p1.BuildReason.Flags);
            Assert.IsTrue(p1.IsDirty);
        }

        [TestMethod]
        public void ApplyStateFile_orders_state_files() {
            var tree = new Mock<IProjectTree>();

            var p1 = new TestConfiguredProject(tree.Object)  {
                outputAssembly = "A",
                IsDirty = false,
                IncludeInBuild = false,
                IsWebProject = true,
                SolutionFile = "MyFile.sln"
            };

            BuildStateFile file1 = new BuildStateFile { BuildId = "1", BucketId = new BucketId("A", "A", BucketVersion.CurrentTree)};
            BuildStateFile file2 = new BuildStateFile { BuildId = "3", BucketId = new BucketId("A", "A", BucketVersion.CurrentTree) };
            BuildStateFile file3 = new BuildStateFile { BuildId = "11", BucketId = new BucketId("A", "A", BucketVersion.CurrentTree) };

            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            sequencer.StateFiles = new List<BuildStateFile> { file1, file3, file2 };

            var applyStateFile = sequencer.SelectStateFiles("A");

            Assert.AreEqual("11", applyStateFile[0].BuildId);
        }

        [TestMethod]
        public void Build_with_no_state_file_and_tracked_files_marks_project_with_InputsChanged() {
            var tree = new Mock<IProjectTree>();

            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "P1",
                IsDirty = false,
                IncludeInBuild = true,
                SolutionFile = "C:\\Repos\\Folder1\\MyFile1.sln",
            };

            var p2 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "P2",
                IsDirty = false,
                IncludeInBuild = true,
                SolutionFile = "C:\\Repos\\Folder2\\MyFile2.sln",
            };

            var p3 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "P3",
                IsDirty = false,
                IncludeInBuild = true,
                SolutionFile = "C:\\Repos\\Folder3\\MyFile2.sln",
            };

            p2.AddResolvedDependency(null, p1);
            p3.AddResolvedDependency(null, p2);

            var sequencer = new ProjectSequencer(NullLogger.Default, null);
            sequencer.TrackedInputFilesCheck = new TestTrackedInputFilesController(new PhysicalFileSystem(), new NullLogger()) {
                Files = new List<TrackedInputFile>(1) { new TrackedInputFile("File") }
            };
            bool hasLoggedUpToDate = false;
            sequencer.ApplyStateFile(null, string.Empty, p1, false, ref hasLoggedUpToDate, null);

            var graph = new ProjectDependencyGraph(p1, p2, p3);

            sequencer.GetProjectsBuildList(graph, new[] { p1, p2, p3 }, null, false, ChangesToConsider.None, DependencyRelationshipProcessing.Direct);

            Assert.AreEqual(BuildReasonTypes.InputsChanged, p1.BuildReason.Flags);
            Assert.IsTrue(p1.IsDirty);
            Assert.IsTrue(p2.IsDirty);
            Assert.AreEqual(BuildReasonTypes.DependencyChanged, p2.BuildReason.Flags);
            Assert.IsNull(p3.BuildReason, "Transitive dependency should not be considered");
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
            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.FileExists(It.IsAny<string>())).Returns(true).Verifiable();
            fs.Setup(s => s.OpenFile("Temp\\Foo\\Build\\DependencyManifest.xml")).Returns("<root />".ToStream());
            fs.Setup(s => s.FileExists(It.Is<string>(s1 => s1.EndsWith(".rsp")))).Returns(false);

            var sequencer = new ProjectSequencer(new NullLogger(), fs.Object);

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

            ProjectSequencer.WriteBuildTree(fileSystem, context.BuildRoot, Resources.BuildTree);

            string output = Path.Combine(TestContext.DeploymentDirectory, "BuildTree.txt");

            Assert.IsTrue(fileSystem.FileExists(output));
            Assert.AreEqual(Resources.BuildTree, fileSystem.ReadAllText(output));
        }

        [TestMethod]
        public void When_a_project_depends_on_a_project_coming_from_cache_it_implicitly_depends_on_the_directory() {
            var tree = new Mock<IProjectTree>();

            var d1 = new DirectoryNode("Dir1", @"C:\Dir1", false);
            var p1 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "P1",
                IsDirty = false,
                IsWebProject = false,
                IncludeInBuild = true,
                SolutionFile = Path.Combine(d1.DirectoryName, "Solution.sln"),
                FullPath = Path.Combine(d1.Directory, "P1.csproj"),
                DirectoryNode = d1
            };

            var d2 = new DirectoryNode("Dir2", @"C:\Dir2", false);
            var p2 = new TestConfiguredProject(tree.Object) {
                outputAssembly = "P2",
                IsDirty = true,
                IsWebProject = false,
                IncludeInBuild = true,
                SolutionFile = Path.Combine(d2.Directory, "Solution.sln"),
                FullPath = Path.Combine(d2.Directory, "P2.csproj"),
                DirectoryNode = d2
            };
            p2.MarkDirtyAndSetReason(BuildReasonTypes.DependencyChanged);
            p2.AddResolvedDependency(null, p1);

            var graph = new ProjectDependencyGraph(d1, p1, d2, p2);

            var projectOutputs1 = new ConcurrentDictionary<string, ProjectOutputSnapshot>();
            projectOutputs1.TryAdd("P1.csproj", new ProjectOutputSnapshot {
                FilesWritten = new string[] { "P1" },
                ProjectFile = "P1.csproj",
                Directory = d1.DirectoryName
            });

            var ctx = new BuildOperationContext {
                BuildRoot = string.Empty,
                Switches = new BuildSwitches {
                    Downstream = true
                },
                BuildMetadata = new BuildMetadata(),
                StateFiles = new List<BuildStateFile> {
                    new BuildStateFile {
                        BucketId = new BucketId(d1.DirectoryName, d1.DirectoryName, BucketVersion.CurrentTree),
                        Artifacts = new ArtifactCollection {
                            { d1.DirectoryName, new List<ArtifactManifest>(1) { new ArtifactManifest() } }
                        },
                        Outputs = projectOutputs1
                    }
                }
            };

            var packageCheckerMock = new Mock<BuildCachePackageChecker>(NullLogger.Default);
            packageCheckerMock.Setup(x => x.DoesArtifactContainProjectItem(It.IsAny<ConfiguredProject>())).Returns(true);

            var sequencer = new ProjectSequencer(NullLogger.Default, new Mock<IFileSystem2>().Object) {
                PackageChecker = packageCheckerMock.Object
            };
            sequencer.CreatePlan(ctx, new OrchestrationFiles(), graph, true, null);

            string[] items = p2.GetDependencies().Select(s => s.Id).ToArray();
            Assert.AreEqual(3, items.Length);
            CollectionAssert.Contains(items, "Dir1.Pre");
            CollectionAssert.Contains(items, "Dir2.Pre");
        }

        [TestMethod]
        public void DisableCacheWhenProjectChanged_marks_project_as_dirty() {
            var tree = new ProjectTreeBuilder();

            var d1 = tree.CreateDirectory("D1", true);

            // A project scheduled to build
            var p1 = tree.CreateProject("P1");
            p1.IsDirty = true;
            p1.SolutionFile = Path.Combine(d1.Directory, "Solution.sln");
            p1.MarkDirtyAndSetReason(BuildReasonTypes.ProjectItemChanged);

            // A project not scheduled to build
            var p2 = tree.CreateProject("P2");
            p2.SolutionFile = Path.Combine(d1.Directory, "Solution.sln");
            p2.IsDirty = false;

            p1.AddResolvedDependency(null, d1);
            p2.AddResolvedDependency(null, d1);

            var graph = new ProjectDependencyGraph(p1, p2, d1);
            var files = new OrchestrationFiles {
                ExtensibilityImposition = { BuildCacheOptions = BuildCacheOptions.DisableCacheWhenProjectChanged }
            };

            var packageCheckerMock = new Mock<BuildCachePackageChecker>(NullLogger.Default);
            packageCheckerMock.Setup(x => x.DoesArtifactContainProjectItem(It.IsAny<ConfiguredProject>())).Returns(true);

            var sequencer = new ProjectSequencer(NullLogger.Default, new Mock<IFileSystem2>().Object) {
                PackageChecker = packageCheckerMock.Object
            };

            sequencer.CreatePlan(tree.CreateContext(), files, graph, false, null);

            Assert.IsTrue(p2.RequiresBuilding());
        }

        [TestMethod]
        public void DoNotDisableCache_does_not_mark_project_as_dirty() {
            var tree = new ProjectTreeBuilder();

            var d1 = tree.CreateDirectory("D1", true);

            // A project scheduled to build
            var p1 = tree.CreateProject("P1");
            p1.SolutionFile = Path.Combine(d1.Directory, "Solution.sln");
            p1.IsDirty = true;
            p1.MarkDirtyAndSetReason(BuildReasonTypes.ProjectItemChanged);
            p1.FullPath = Path.Combine(d1.Directory, "P1", "P1.csproj");

            // A project not scheduled to build
            var p2 = tree.CreateProject("P2");
            p2.SolutionFile = Path.Combine(d1.Directory, "Solution.sln");
            p2.IsDirty = false;
            p2.FullPath = Path.Combine(d1.Directory, "P2", "P2.csproj");
            // p2.DirectoryNode = d1;

            p1.AddResolvedDependency(null, d1);
            p2.AddResolvedDependency(null, d1);

            var graph = new ProjectDependencyGraph(p1, p2, d1);
            var files = new OrchestrationFiles {
                ExtensibilityImposition = {BuildCacheOptions = BuildCacheOptions.DoNotDisableCacheWhenProjectChanged}
            };

            var projectOutputs1 = new ConcurrentDictionary<string, ProjectOutputSnapshot>();
            projectOutputs1.TryAdd("P2", new ProjectOutputSnapshot {
                FilesWritten = new string[] { "P2" },
                ProjectFile = @"P2\P2.csproj",
                Directory = d1.DirectoryName
            });

            var context = new BuildOperationContext {
                BuildRoot = string.Empty,
                Switches = new BuildSwitches {
                    Downstream = true
                },
                BuildMetadata = new BuildMetadata(),
                StateFiles = new List<BuildStateFile> {
                    new BuildStateFile {
                        BucketId = new BucketId("P2", d1.DirectoryName, BucketVersion.CurrentTree),
                        Artifacts = new ArtifactCollection {
                            { d1.DirectoryName, new List<ArtifactManifest>(1) { new ArtifactManifest() } }
                        },
                        Outputs = projectOutputs1
                    }
                }
            };

            var packageCheckerMock = new Mock<BuildCachePackageChecker>(NullLogger.Default);
            packageCheckerMock.Setup(x => x.DoesArtifactContainProjectItem(It.IsAny<ConfiguredProject>())).Returns(true);

            var sequencer = new ProjectSequencer(NullLogger.Default, new Mock<IFileSystem2>().Object) {
                PackageChecker = packageCheckerMock.Object
            };

            sequencer.CreatePlan(context, files, graph, true, null);

            Assert.IsFalse(p2.RequiresBuilding());
        }

        [TestMethod]
        public void When_all_projects_in_cone_dirty_artifact_download_is_disabled() {
            var tree = new ProjectTreeBuilder();

            var d1 = tree.CreateDirectory("D1", true);
            var d2 = tree.CreateDirectory("D2", true);

            var p1 = tree.CreateProject("P1");
            p1.SolutionFile = Path.Combine(d1.Directory, "Solution.sln");
            p1.IsDirty = true;
            p1.MarkDirtyAndSetReason(BuildReasonTypes.ProjectItemChanged);
            p1.FullPath = Path.Combine(d1.Directory, "P1", "P1.csproj");
            p1.DirectoryNode = d1;

            // A project scheduled to build via upstream change
            var p2 = tree.CreateProject("P2");
            p2.SolutionFile = Path.Combine(d2.Directory, "Solution.sln");
            p2.IsDirty = true;
            p2.MarkDirtyAndSetReason(BuildReasonTypes.InputsChanged);
            p2.FullPath = Path.Combine(d2.Directory, "P2", "P2.csproj");
            p2.DirectoryNode = d2;

            p1.AddResolvedDependency(null, d1);
            p2.AddResolvedDependency(null, d2);
            p2.AddResolvedDependency(null, p1);

            var graph = new ProjectDependencyGraph(p1, p2, d1, d2);

            var sequencer = new ProjectSequencer(NullLogger.Default, new Mock<IFileSystem2>().Object) {
                PackageChecker = new Mock<BuildCachePackageChecker>(NullLogger.Default).Object
            };

            Assert.IsFalse(d2.RetrievePrebuilts.HasValue);

            sequencer.SecondPassAnalysis(graph.Nodes.ToList(), graph, BuildCacheOptions.DoNotDisableCacheWhenProjectChanged);

            Assert.IsFalse(d1.RetrievePrebuilts);
            Assert.IsFalse(d2.RetrievePrebuilts);
        }

        [TestMethod]
        public void FilterStateFiles() {
            var projectSequencer = new ProjectSequencer(NullLogger.Default, null);

            const string packageHash = "BBED1D49615C071DAAAD48AD1FC057E1112148D0";

            Guid chosenGuid = Guid.NewGuid();

            BuildStateFile[] buildStateFiles = new BuildStateFile[] {
                new BuildStateFile(),
                new BuildStateFile {
                    Id = chosenGuid,
                    PackageHash = packageHash
                }
            };

            BuildStateFile[] files = projectSequencer.FilterStateFiles(buildStateFiles, packageHash);

            Assert.IsNotNull(files);
            Assert.AreEqual(packageHash, files[0].PackageHash);
            Assert.AreEqual(chosenGuid, files[0].Id);
        }

        [TestMethod]
        public void FilterStateFilesNoPackageHashMatch() {
            var projectSequencer = new ProjectSequencer(NullLogger.Default, null);

            const string packageHash = "BBED1D49615C071DAAAD48AD1FC057E1112148D0";

            Guid chosenGuid = Guid.NewGuid();

            BuildStateFile[] buildStateFiles = new BuildStateFile[] {
                new BuildStateFile {
                    Id = chosenGuid
                },
                new BuildStateFile()
                };

            BuildStateFile[] files = projectSequencer.FilterStateFiles(buildStateFiles, packageHash);

            Assert.IsNotNull(files);
            Assert.AreNotEqual(packageHash, files[0].PackageHash);
            Assert.AreEqual(chosenGuid, files[0].Id);
        }

        [TestMethod]
        public void SequencerFiltersBuildStateFilesAppropriately() {
            var p1 = new TestConfiguredProject(null) {
                outputAssembly = "TestProject1.dll",
                IncludeInBuild = true,
                DirectoryNode = new DirectoryNode("A", "Test1", false),
                SolutionFile = @"C:\Source\Test1\Test1.sln",
                FullPath = @"C:\Source\Test1\Src\TestProject1.csproj"
            };

            var p2 = new TestConfiguredProject(null) {
                outputAssembly = "TestProject2.dll",
                IncludeInBuild = true,
                ProjectTypeGuids = new[] { WellKnownProjectTypeGuids.TestProject },
                DirectoryNode = new DirectoryNode("B", "Test1", false),
                SolutionFile = @"C:\Source\Test2\Test2.sln",
                FullPath = @"C:\Source\Test2\Src\TestProject2.csproj"
            };

            var projectOutputs1 = new ConcurrentDictionary<string, ProjectOutputSnapshot>();
            projectOutputs1.TryAdd("TestProject1.csproj", new ProjectOutputSnapshot {
                Directory = @"Test1\Src",
                FilesWritten = new string[] { "TestProject1.dll" },
                ProjectFile = "TestProject1.csproj"
            });

            var projectOutputs2 = new ConcurrentDictionary<string, ProjectOutputSnapshot>();
            projectOutputs2.TryAdd("TestProject2.csproj", new ProjectOutputSnapshot {
                Directory = @"Test2\Src",
                FilesWritten = new string[] { "TestProject2.dll" },
                ProjectFile = "TestProject2.csproj"
            });

            var context = new BuildOperationContext {
                BuildRoot = string.Empty,
                Switches = new BuildSwitches {
                    Downstream = true
                },
                BuildMetadata = new BuildMetadata(),
                StateFiles = new List<BuildStateFile> {
                    new BuildStateFile {
                        BucketId = new BucketId("A", "Test1", BucketVersion.CurrentTree),
                        Artifacts = new ArtifactCollection {
                            { "Test1", new List<ArtifactManifest>(1) {
                                new ArtifactManifest {
                                    Id = "TestProject1"
                                }
                            } }
                        },
                        Outputs = projectOutputs1
                    },
                    new BuildStateFile {
                        BucketId = new BucketId("A", "Test1", BucketVersion.CurrentTree),
                        Artifacts = new ArtifactCollection {
                            { "Test1", new List<ArtifactManifest>(0) }
                        },
                        Outputs = projectOutputs1
                    },
                    new BuildStateFile {
                        BucketId = new BucketId("A", "Test1", BucketVersion.PreviousTree),
                        Artifacts = new ArtifactCollection {
                            { "Test2", new List<ArtifactManifest>(0) }
                        },
                        Outputs = projectOutputs2
                    },
                    new BuildStateFile {
                        BucketId = new BucketId("B", "Test2", BucketVersion.CurrentTree),
                        Artifacts = new ArtifactCollection {
                            { "Test2", new List<ArtifactManifest>(0) }
                        },
                        Outputs = projectOutputs2
                    },
                    new BuildStateFile {
                        BucketId = new BucketId("B", "Test2", BucketVersion.PreviousTree)
                    }
                }
            };

            var fileSystemMock = new Mock<PhysicalFileSystem>();
            fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

            var packageCheckerMock = new Mock<BuildCachePackageChecker>(NullLogger.Default);
            packageCheckerMock.Setup(x => x.DoesArtifactContainProjectItem(It.IsAny<ConfiguredProject>())).Returns(true);

            var graph = new ProjectDependencyGraph(p1, p2);
            var sequencer = new ProjectSequencer(NullLogger.Default, fileSystemMock.Object) {
                PackageChecker = packageCheckerMock.Object
            };
            sequencer.CreatePlan(context, new OrchestrationFiles(), graph, true, null);

            Assert.IsNotNull(context.StateFiles);
            Assert.AreEqual(bool.TrueString, context.Variables["IsBuildCacheEnabled"]);

            Assert.AreEqual(2, context.StateFiles.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(DuplicateGuidException))]
        public void DuplicateGuidValidationTest() {
            ProjectTreeBuilder projectTreeBuilder = new ProjectTreeBuilder();

            Guid guid = Guid.NewGuid();

            ConfiguredProject projectA = projectTreeBuilder.CreateProjectWithGuid("a", guid);
            ConfiguredProject projectB = projectTreeBuilder.CreateProjectWithGuid("b", guid);

            ProjectTree projectTree = new ProjectTree(new TextContextLogger(this.TestContext));

            projectTree.AddConfiguredProject(projectA);
            projectTree.AddConfiguredProject(projectB);
        }

        [TestMethod]
        public void GuidValidationSdkStyleProjectTest() {
            ProjectTreeBuilder projectTreeBuilder = new ProjectTreeBuilder();

            ConfiguredProject projectA = projectTreeBuilder.CreateProject("a");
            projectA.IsSdkStyeProject = true;
            ConfiguredProject projectB = projectTreeBuilder.CreateProject("b");
            projectB.IsSdkStyeProject = true;

            ProjectTree projectTree = new ProjectTree(new TextContextLogger(this.TestContext));

            projectTree.AddConfiguredProject(projectA);
            projectTree.AddConfiguredProject(projectB);

            Assert.AreEqual(2, projectTree.LoadedConfiguredProjects.Count);
        }
    }

    internal class ProjectTreeBuilder {
        readonly IProjectTree tree = new Mock<IProjectTree>().Object;

        public BuildOperationContext CreateContext() {
            return new BuildOperationContext {
                BuildRoot = string.Empty,
                Switches = new BuildSwitches {
                    Downstream = true
                },
                BuildMetadata = new BuildMetadata(),
                StateFiles = new List<BuildStateFile> { new BuildStateFile() }
            };
        }

        public ConfiguredProject CreateProjectWithGuid(string name, Guid guid) {
            return new TestConfiguredProject(tree, guid) {
                outputAssembly = name,
                IsDirty = false,
                IsWebProject = false,
                IncludeInBuild = true
            };
        }

        public ConfiguredProject CreateProject(string name) {
            return new TestConfiguredProject(tree) {
                outputAssembly = name,
                IsDirty = false,
                IsWebProject = false,
                IncludeInBuild = true
            };
        }

        public DirectoryNode CreateDirectory(string name, bool prologue) {
            return new DirectoryNode(name, $"file://{name}", !prologue);
        }
    }
}
