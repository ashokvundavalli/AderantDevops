using System;
using System.IO;
using System.Text;
using Aderant.Build;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Tasks;
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
        public void BuildStateFile_serialization() {
            var stateFile = new BuildStateFile();
            stateFile.Outputs = new ProjectOutputSnapshot { { "Foo", new OutputFilesSnapshot() } };
            stateFile.Artifacts = new ArtifactCollection();
            stateFile.PullRequestInfo = new PullRequestInfo {
                Id = "1", SourceBranch = "2", TargetBranch = "3"
            };

            using (var ms = new MemoryStream()) {
                stateFile.Serialize(ms);

                ms.Position = 0;

                BuildStateFile file = stateFile.DeserializeCache<BuildStateFile>(ms);

                Assert.AreEqual("1", file.PullRequestInfo.Id);
                Assert.AreEqual("2", file.PullRequestInfo.SourceBranch);
                Assert.AreEqual("3", file.PullRequestInfo.TargetBranch);
            }
        }

        [TestMethod]
        public void Deserialize_throws_no_exceptions() {
            using (var ms = new MemoryStream(Encoding.Default.GetBytes(Resources.buildstate))) {
                ms.Position = 0;
                BuildStateFile file = new BuildStateFile().DeserializeCache<BuildStateFile>(ms);
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

            var writer = new BuildStateWriter(fs.Object);

            writer.WriteStateFile(
                null,
                null,
                null,
                null,
                null,
                null,
                "foo");

            Assert.IsNotNull(text);
        }
    }
}
