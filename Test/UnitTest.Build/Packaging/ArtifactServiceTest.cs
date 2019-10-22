using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.Handlers;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class ArtifactServiceTest {

        [TestMethod]
        public void CreateArtifacts() {
            var bucketMock = new Mock<IBucketPathBuilder>();
            bucketMock.Setup(s => s.GetBucketId(It.IsAny<string>())).Returns("");

            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.FileExists("Baz")).Returns(true);
            fs.Setup(s => s.BulkCopy(It.IsAny<IEnumerable<PathSpec>>(), true, false, true)).Returns(
                () => {
                    var block = new ActionBlock<PathSpec>(spec => { });
                    block.Complete();
                    return block;
                });

            var artifactService = new ArtifactService(new BuildPipelineServiceImpl(), fs.Object, NullLogger.Default);
            artifactService.RegisterHandler(new XamlDropHandler("1.0.0.0", "9.9.9.9"));

            IEnumerable<PathSpec> specs = new List<PathSpec> { new PathSpec("Baz", "") };

            IReadOnlyCollection<BuildArtifact> results = artifactService.CreateArtifacts(
                new BuildOperationContext {
                    ArtifactStagingDirectory = @"\\mydrop\foo",
                    SourceTreeMetadata = new SourceTreeMetadata(),
                    BuildMetadata = new BuildMetadata()
                },
                "Bar",
                new[] { new ArtifactPackageDefinition("bar", specs) });

            Assert.IsNotNull(results);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Double_writes_in_artifacts_are_detected() {
            var artifactService = new ArtifactService(NullLogger.Default);
            artifactService.CheckForDuplicates(
                "Foo",
                new[] {
                    new PathSpec(@"ABC\Z.dll", "Z.dll"),
                    new PathSpec(@"DEF\Z.dll", "Z.dll"),
                });
        }

        [TestMethod]
        public void CalculateFilesToRestore_returns_full_path() {
            var state = new BuildStateFile();
            state.Outputs = new ProjectTreeOutputSnapshot();
            state.Outputs["Foo\\Bar.cspoj"] = new ProjectOutputSnapshot {
                Directory = "Foo",
                FilesWritten = new string[] {
                    @"..\..\bin\foo.dll"
                },
                OutputPath = @"..\..\bin\"
            };

            var fsMock = new Mock<IFileSystem>();
            fsMock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(true);

            var artifactService = new ArtifactService(null, fsMock.Object, NullLogger.Default);
            var result = artifactService.CalculateFilesToRestore(state, "Foo", "Foo", new[] { @"\Foo.dll", @"\Foo.pdb" });

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].Destination.EndsWith(@"bin\foo.dll"));
            Assert.IsTrue(Path.IsPathRooted(result[0].Destination));
        }

        [TestMethod]
        public void CreateLinkCommands() {
            var mock = new Mock<IBuildPipelineService>();
            mock.Setup(s => s.GetAssociatedArtifacts()).Returns(
                new[] {
                    new BuildArtifact {
                        Name = "SomeOtherArtifact",
                        SourcePath = @"C:\Foo\_artifacts\SomeOtherArtifactOnDisk\Stuff",
                        SendToArtifactCache = true
                    }
                });

            var artifactService = new ArtifactService(mock.Object, new Mock<IFileSystem>().Object, NullLogger.Default);

            var linkCommands = artifactService.GetPublishCommands(
                @"C:\Foo",
                new DropLocationInfo {
                    PrimaryDropLocation = @"\\foo\bar",
                    BuildCacheLocation = @"\\baz\cache"
                },
                new BuildMetadata {
                    ScmBranch = "refs/heads/master"
                },
                new[] {
                    new ArtifactPackageDefinition("TheProduct", new[] { new PathSpec(@"C:\Foo\MyProduct.zip", "") }) {
                        ArtifactType = ArtifactType.Branch

                    }
                },
                false);

            Assert.IsNotNull(linkCommands);

            PathSpec spec1 = linkCommands.ArtifactPaths.SingleOrDefault(s => s.Location == @"C:\Foo\MyProduct.zip");
            Assert.IsTrue(string.Equals(@"\\foo\bar\refs\heads\master\0\TheProduct", spec1.Destination, StringComparison.OrdinalIgnoreCase));

            PathSpec spec2 = linkCommands.ArtifactPaths.SingleOrDefault(s => s.Location == @"C:\Foo\_artifacts\SomeOtherArtifactOnDisk\Stuff");
            Assert.IsTrue(string.Equals(@"\\baz\cache\SomeOtherArtifactOnDisk\Stuff", spec2.Destination, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [Description("Confirms that we do not append the artifact name to the link path. This is critical to the proper functioning of the TFS garbage collector which appends the name when running a prune operation")]
        public void Artifact_association_command_does_not_end_with_artifact_name() {
            var mock = new Mock<IBuildPipelineService>();
            mock.Setup(s => s.GetAssociatedArtifacts()).Returns(
                new[] {
                    new BuildArtifact { Name = "~A", SourcePath = @"C:\Foo\_artifacts\~A", SendToArtifactCache = true },
                    new BuildArtifact { Name = "SomeOtherArtifact", SourcePath = @"C:\Foo\_artifacts\SomeOtherArtifactOnDisk\Stuff", SendToArtifactCache = true },
                });

            var artifactService = new ArtifactService(mock.Object, new Mock<IFileSystem>().Object, NullLogger.Default);

            var linkCommands = artifactService.GetPublishCommands(
                @"C:\Foo",
                new DropLocationInfo {
                    PrimaryDropLocation = @"\\foo\bar",
                    BuildCacheLocation = @"\\baz\cache"
                },
                new BuildMetadata {
                    ScmBranch = "refs/heads/master"
                },
                new[] {
                    new ArtifactPackageDefinition("TheProduct", new[] { new PathSpec(@"C:\Foo\MyProduct.zip", "") }) {
                        ArtifactType = ArtifactType.Branch

                    }
                },
                false);

            Assert.IsNotNull(linkCommands);

            Assert.IsTrue(linkCommands.AssociationCommands.Contains(@"##vso[artifact.associate artifactname=TheProduct;type=FilePath;]\\foo\bar\refs\heads\master\0", StringComparer.OrdinalIgnoreCase));
            Assert.IsTrue(linkCommands.AssociationCommands.Contains(@"##vso[artifact.associate artifactname=SomeOtherArtifact;type=FilePath;]\\baz\cache", StringComparer.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ZeroId_is_allowed_for_tests() {
            var mock = new Mock<IBuildPipelineService>();
            var artifactService = new ArtifactService(mock.Object, new Mock<IFileSystem>().Object, NullLogger.Default);
            artifactService.AllowZeroBuildId = true;

            var result = artifactService.OrderBuildsByBuildNumber(new[] { "0", "5", "8" });

            CollectionAssert.AreEquivalent(new[] { "8", "5", "0" }, result);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void CreateBuildCacheArtifact_throws_when_source_file_is_missing() {
            var fsMock = new Mock<IFileSystem>();
            fsMock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(false);

            var definition = new ArtifactPackageDefinitionBuilder("MyPackage", (a) => { a.AddFile("Alpha", ""); }).Build();

            var artifactService = new ArtifactService(null, fsMock.Object, NullLogger.Default);
            artifactService.SetPathBuilder(new ArtifactStagingPathBuilder("", 1, new SourceTreeMetadata()));

            var paths = artifactService.CreateBuildCacheArtifact(
                "",
                new[] { PathSpec.Create("A", "") },
                definition,
                definition.GetFiles());
        }
    }
}