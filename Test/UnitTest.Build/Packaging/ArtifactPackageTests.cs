﻿using System;
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
    public class ArtifactPackageTests {

        [TestMethod]
        public void Vso_storage_path_is_full_path_minus_name() {
            // Damn build systems. So you would think that TFS would take the path verbatim and just store that away.
            // But no, it takes the UNC path you give it and then when the garbage collection occurs it appends the artifact name as a folder
            // to that original path as the final path to delete. 
            // This means the web UI for a build will always point to the root folder, which is useless for usability and we need to 
            // set the actual final folder as the name.

            BuildArtifact storageInfo = new BuildArtifact {
                FullPath = @"\\some\san\storage\1\foo\bin",
                Name = @"1\foo\bin"
            };

            string vsoPath = storageInfo.ComputeVsoPath();

            Assert.AreEqual(@"\\some\san\storage", vsoPath);
        }

        [TestMethod]
        public void Vso_path_is_smallest_substring() {
            BuildArtifact a = new BuildArtifact {
                FullPath = "\\\\mydrop\\bar\\9.9.9.9\\1.0.0.0\\Bin\\Module",
                Name = "bar\\9.9.9.9\\1.0.0.0"
            };

            var computeVsoPath = a.ComputeVsoPath();

            Assert.AreEqual("\\\\mydrop", computeVsoPath);
        }

        [TestMethod]
        public void PublishArtifacts() {
            var bucketMock = new Mock<IBucketPathBuilder>();
            bucketMock.Setup(s => s.GetBucketId(It.IsAny<string>())).Returns("");

            var artifactService = new ArtifactService(null, new Mock<IFileSystem>().Object, NullLogger.Default);
            artifactService.RegisterHandler(new XamlDropHandler("1.0.0.0", "9.9.9.9"));

            IEnumerable<PathSpec> specs = new List<PathSpec> { new PathSpec("Baz", null) };

            IReadOnlyCollection<BuildArtifact> results = artifactService.PublishArtifacts(
                new BuildOperationContext {
                    Drops = { PrimaryDropLocation = @"\\mydrop\" },
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
            state.Outputs = new ProjectOutputSnapshot();
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
        public void Destination_is_filename_by_default() {
            PathSpec spec = ArtifactPackageDefinition.CreatePathSpecification(null, null, @"Foo\Bar\Baz.dll", null);
            Assert.AreEqual("Baz.dll", spec.Destination);
        }

        [TestMethod]
        public void Destination_is_respected_when_directory() {
            PathSpec spec = ArtifactPackageDefinition.CreatePathSpecification(null, null, @"Foo\Bar\Baz.dll", "Foo");
            Assert.AreEqual(@"Foo\Baz.dll", spec.Destination);
        }

        
    }

    [TestClass]
    public class TestPackageBuilderTest {

        [TestMethod]
        public void Foo() {
            var builder = new TestPackageBuilder();

            var filesToPackage = new PathSpec[] {
                new PathSpec("Foo.dll", @"Foo\Bar.dll"),
            };

            var snapshot = new ProjectOutputSnapshot();
            snapshot[""] = new OutputFilesSnapshot();
            snapshot[""].IsTestProject = true;
            snapshot[""].FilesWritten = new[] { "Foo.dll" };
            snapshot[""].Directory = "";

            var files = builder.BuildArtifact(filesToPackage, snapshot, "");

            Assert.AreEqual(1, files.Count);
        }
    }
}
