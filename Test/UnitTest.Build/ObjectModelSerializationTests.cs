using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace UnitTest.Build {
    [TestClass]
    public class ObjectModelSerializationTests {

        [TestMethod]
        public void BuildArtifactSerialization() {
            var artifact = new BuildArtifact();
            artifact.FullPath = "Abc";

            var instance = RoundTrip(artifact);

            Assert.AreEqual("Abc", instance.FullPath);
        }

        [TestMethod]
        public void BuildStateMetadataSerialization() {
            var metadata = new BuildStateMetadata();
            metadata.BuildStateFiles = new List<BuildStateFile> { new BuildStateFile { BuildId = "1" } };

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance.BuildStateFiles);
            Assert.AreEqual(1, instance.BuildStateFiles.Count);
            Assert.AreEqual("1", instance.BuildStateFiles.First().BuildId);
        }

        [TestMethod]
        public void BuildOperationContextSerialization() {
            var metadata = new BuildOperationContext();
            metadata.WrittenStateFiles = new List<string> { "1"  };

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, instance.WrittenStateFiles.Count);
        }

        [TestMethod]
        public void BuildStateFileSerialization() {
            var metadata = new BuildStateFile();

            metadata.Artifacts = new Dictionary<string, ICollection<ArtifactManifest>>();
            metadata.Artifacts["Foo"] = new List<ArtifactManifest> { new ArtifactManifest { Id = "Bar" } };

            metadata.Outputs = new Dictionary<string, OutputFilesSnapshot>();
            metadata.Outputs["Baz"] = new OutputFilesSnapshot { Directory = "Gaz" };

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, instance.Artifacts.Keys.Count);
            Assert.AreEqual("Bar", instance.Artifacts["Foo"].First().Id);

            Assert.AreEqual(1, instance.Outputs.Keys.Count);
            Assert.AreEqual("Gaz", instance.Outputs["Baz"].Directory);
        }

        private static T RoundTrip<T>(T artifact) where T : class {
            return ProtoDeserialize<T>(ProtoSerialize(artifact));
        }

        public static T ProtoDeserialize<T>(byte[] data) where T : class {
            using (var stream = new MemoryStream(data)) {
                return Serializer.Deserialize<T>(stream);
            }
        }

        public static byte[] ProtoSerialize<T>(T graph) where T : class {
            using (var stream = new MemoryStream()) {
                Serializer.Serialize(stream, graph);
                stream.Position = 0;
                return stream.ToArray();
            }
        }
    }
}
