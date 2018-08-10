﻿using System;
using System.IO;
using Aderant.Build;
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
            stateFile.PullRequestInfo = new PullRequestInfo { Id = "1", SourceBranch = "2", TargetBranch = "3" };

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
        public void Can_write_build_state_file_to_stream() {
            string text = null;

            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.AddFile(It.IsAny<string>(), It.IsAny<Action<Stream>>())).Callback<string, Action<Stream>>(
                (s, s1) => {
                    using (var ms = new MemoryStream()) {
                        s1(ms);

                        ms.Position = 0;
                        using (var reader = new StreamReader(ms)) {
                            text = reader.ReadToEnd();
                        }
                    }
                });

            var writer = new BuildStateWriter(fs.Object);

            writer.WriteStateFile(
                string.Empty,
                new[] {
                    "A\\A.csproj",
                    "A\\B.csproj"
                },
                null,
                null,
                null,
                "foo");

            Assert.IsNotNull(text);
        }
    }
}
