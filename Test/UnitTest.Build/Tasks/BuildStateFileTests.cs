using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem.StateTracking;
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
                "foo");

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
                "foo");

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
                new Dictionary<string, ICollection<ArtifactManifest>> { {"Test", new List<ArtifactManifest> { new ArtifactManifest() }} },
                null,
                null,
                "foo");

            Assert.IsNotNull(text);
        }
    }
}
