using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class BuildStateFileTests {

        [TestMethod]
        public void Version_serialization() {
            Assert.IsNotNull(new StateFileBase().serializedVersion);
        }

        [TestMethod]
        public void Location_property_is_no_preserved() {
            var metadata = new BuildStateFile();

            metadata.Location = "Abc";

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.IsNull(instance.Location);
        }

        [TestMethod]
        public void BuildStateFile_serialization() {
            var stateFile = new BuildStateFile();
            stateFile.Outputs = new ProjectTreeOutputSnapshot { { "Foo", new ProjectOutputSnapshot() } };
            stateFile.Artifacts = new ArtifactCollection();
            stateFile.PullRequestInfo = new PullRequestInfo {
                Id = "1", SourceBranch = "2", TargetBranch = "3"
            };

            var instance = RoundTrip(stateFile);

            Assert.AreEqual("1", instance.PullRequestInfo.Id);
            Assert.AreEqual("2", instance.PullRequestInfo.SourceBranch);
            Assert.AreEqual("3", instance.PullRequestInfo.TargetBranch);

        }

        private static T RoundTrip<T>(T artifact) where T : StateFileBase {
            return Deserialize<T>(Serialize(artifact));
        }

        private static T Deserialize<T>(byte[] data) where T : StateFileBase {
            using (var stream = new MemoryStream(data)) {
                return StateFileBase.DeserializeCache<T>(stream);
            }
        }

        public static byte[] Serialize<T>(T graph) where T : StateFileBase {
            using (var stream = new MemoryStream()) {
                graph.Serialize(stream);
                stream.Position = 0;
                return stream.ToArray();
            }
        }

        [TestMethod]
        public void Deserialize_throws_no_exceptions() {
            using (var ms = new MemoryStream(Encoding.Default.GetBytes(Resources.buildstate))) {
                ms.Position = 0;
                BuildStateFile file = StateFileBase.DeserializeCache<BuildStateFile>(ms);
            }
        }

        [TestMethod]
        public void Can_write_build_state_file_to_stream() {
            string text = null;

            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.AddFile(It.IsAny<string>(), It.IsAny<Action<Stream>>())).Callback<string, Action<Stream>>(
                (_, action) => {
                    using (var ms = new MemoryStream()) {
                        action(ms);

                        ms.Position = 0;
                        using (var reader = new StreamReader(ms)) {
                            text = reader.ReadToEnd();
                        }
                    }
                });

            var writer = new BuildStateWriter(fs.Object, NullLogger.Default);

            writer.WriteStateFile(
                null,
                null,
                new[] { new ProjectOutputSnapshot { ProjectFile = "p1" } },
                null,
                null,
                null,
                null,
                null);

            Assert.IsNotNull(text);
        }

        [TestMethod]
        public void BuildStateWriterReturnsNullWithNoOutputsOrArtifactsPresent() {
            string text = null;

            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.AddFile(It.IsAny<string>(), It.IsAny<Action<Stream>>())).Callback<string, Action<Stream>>(
                (_, action) => {
                    using (var ms = new MemoryStream()) {
                        action(ms);

                        ms.Position = 0;
                        using (var reader = new StreamReader(ms)) {
                            text = reader.ReadToEnd();
                        }
                    }
                });

            var writer = new BuildStateWriter(fs.Object, NullLogger.Default);

            writer.WriteStateFile(
                null,
                null,
                new ProjectOutputSnapshot[0],
                null,
                null,
                null,
                null,
                null);

            Assert.IsNull(text);
        }

        [TestMethod]
        public void BuildStateWriterReturnsValueWithArtifacts() {
            string text = null;

            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.AddFile(It.IsAny<string>(), It.IsAny<Action<Stream>>())).Callback<string, Action<Stream>>(
                (_, action) => {
                    using (var ms = new MemoryStream()) {
                        action(ms);

                        ms.Position = 0;
                        using (var reader = new StreamReader(ms)) {
                            text = reader.ReadToEnd();
                        }
                    }
                });

            var writer = new BuildStateWriter(fs.Object, NullLogger.Default);

            writer.WriteStateFile(
                null,
                null,
                new ProjectOutputSnapshot[0],
                null,
                new Dictionary<string, ICollection<ArtifactManifest>> { {"Test", new List<ArtifactManifest> { new ArtifactManifest() }} },
                null,
                null,
                null);

            Assert.IsNotNull(text);
        }

        [TestMethod]
        public void BuildStateWriter_removes_transitive_baggage() {
            // Since we aren't using the common output directory for tests, the FilesWritten array gets a lot of transitive baggage included.
            // For example each test project wants to copy log4net.xml to the output directory.
            // This results in a double write warning when we replay the build. Stink.
            // If we tell it to use a common output directory for tests we get other problems though such as missing dependency DLLs which results in test failures.
            // The transitive baggage prune step unifies test project output to resolve this issue
            var fs = new Mock<IFileSystem>();

            var writer = new BuildStateWriter(fs.Object, NullLogger.Default);

            BuildStateFile stateFile;

            writer.WriteStateFile(
                null,
                null,
                new[] {
                    new ProjectOutputSnapshot {
                        ProjectFile = "1",
                        FilesWritten = new[] {"A", "B"},
                        IsTestProject = true,
                        Directory = "",
                        OutputPath = "bin\\test",
                    },
                    new ProjectOutputSnapshot {
                        ProjectFile = "2",
                        FilesWritten = new[] {"A", "C"},
                        IsTestProject = true,
                        Directory = "",
                        OutputPath = "bin\\test",
                    }
                },
                null,
                null,
                null,
                null,
                null,
                out stateFile);

            var projectOutputSnapshots = stateFile.Outputs.Values.ToArray();
            Assert.AreEqual(2, projectOutputSnapshots[0].FilesWritten.Length, "item 'A' should not have been pruned");
            Assert.AreEqual(1, projectOutputSnapshots[1].FilesWritten.Length, "item 'A' should have been pruned");
        }
    }
}
