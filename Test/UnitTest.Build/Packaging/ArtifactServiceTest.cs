using System;
using System.Collections.Generic;
using System.IO;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.Handlers;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class ArtifactServiceTest {

        [TestMethod]
        public void CreateArtifacts() {
            var bucketMock = new Mock<IBucketPathBuilder>();
            bucketMock.Setup(s => s.GetBucketId(It.IsAny<string>())).Returns("");

            var artifactService = new ArtifactService(null, new Mock<IFileSystem>().Object, NullLogger.Default);
            artifactService.RegisterHandler(new XamlDropHandler("1.0.0.0", "9.9.9.9"));

            IEnumerable<PathSpec> specs = new List<PathSpec> { new PathSpec("Baz", null) };

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
            state.Outputs["Foo\\Bar.cspoj"] = new OutputFilesSnapshot {
                FilesWritten = new string[] {
                    @"..\..\bin\foo.dll"
                }
            };

            var fsMock = new Mock<IFileSystem>();
            fsMock.Setup(s => s.FileExists(It.IsAny<string>())).Returns(true);

            var artifactService = new ArtifactService(null, fsMock.Object, NullLogger.Default);
            var result = artifactService.CalculateFilesToRestore(state, "Foo", "Foo", new[] { "Foo.dll", "Foo.pdb" });

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].Destination.EndsWith(@"bin\foo.dll"));
            Assert.IsTrue(Path.IsPathRooted(result[0].Destination));
        }

        [TestMethod]
        public void CreateLinkCommands() {
            var artifactService = new ArtifactService(null, new Mock<IFileSystem>().Object, NullLogger.Default);

            //artifactService.CreateLinkCommands(
            //    @"C:\Foo",
            //    @"\\some\location\",
            //    new BuildArtifact[] {
            //        new BuildArtifact {
            //            Name = "TheProduct",
            //            FullPath = @"C:\SomeLocation\OnDisk.zip"
            //        }
            //    }, 123);
        }
    }

}
