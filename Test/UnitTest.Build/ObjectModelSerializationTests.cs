using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace UnitTest.Build {
    [TestClass]
    public class ObjectModelSerializationTests {

        [TestMethod]
        public void BuildArtifactSerialization() {
            var artifact = new BuildArtifact();
            artifact.SourcePath = "Abc";

            var instance = RoundTrip(artifact);

            Assert.AreEqual("Abc", instance.SourcePath);
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
        public void BuildSwitchesSerialization() {
            var metadata = new BuildSwitches();
            metadata.Clean = true;

            var instance = RoundTrip(metadata);

            Assert.IsTrue(instance.Clean);
        }

        [TestMethod]
        public void BuildOperationContextSerialization() {
            var metadata = new BuildOperationContext();
            metadata.WrittenStateFiles = new List<string> { "1" };
            metadata.IsDesktopBuild = false;

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, instance.WrittenStateFiles.Count);
            Assert.IsFalse(instance.IsDesktopBuild);
        }

        [TestMethod]
        public void SourceChangeSerialization() {
            var change = new SourceChange("A", "B", FileStatus.Added);
            var instance = RoundTrip(change);

            Assert.IsNotNull(instance);
            Assert.AreEqual("B", instance.Path);
        }

        [TestMethod]
        public void BuildStateFileSerialization() {
            var metadata = new BuildStateFile();

            metadata.Artifacts = new Dictionary<string, ICollection<ArtifactManifest>>();
            metadata.Artifacts["Foo"] = new List<ArtifactManifest> { new ArtifactManifest { Id = "Bar" } };

            metadata.Outputs = new Dictionary<string, ProjectOutputSnapshot>();
            metadata.Outputs["Baz"] = new ProjectOutputSnapshot { Directory = "Gaz" };

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(StateFileBase.CurrentSerializationVersion, instance.serializedVersion);

            Assert.AreEqual(1, instance.Artifacts.Keys.Count);
            Assert.AreEqual("Bar", instance.Artifacts["Foo"].First().Id);

            Assert.AreEqual(1, instance.Outputs.Keys.Count);
            Assert.AreEqual("Gaz", instance.Outputs["Baz"].Directory);
            Assert.IsInstanceOfType(instance.Outputs, typeof(ProjectTreeOutputSnapshot));
        }


        [TestMethod]
        public void Outputs_is_case_insensitive() {
            var metadata = new BuildStateFile();

            metadata.Artifacts = new Dictionary<string, ICollection<ArtifactManifest>>();
            metadata.Artifacts["Foo"] = new List<ArtifactManifest> { new ArtifactManifest { Id = "Bar" } };

            metadata.Outputs = new Dictionary<string, ProjectOutputSnapshot>();
            metadata.Outputs["Baz"] = new ProjectOutputSnapshot { Directory = "Gaz" };

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(StateFileBase.CurrentSerializationVersion, instance.serializedVersion);

            Assert.IsTrue(instance.Artifacts.ContainsKey("foo"));
            Assert.IsTrue(instance.Outputs.ContainsKey("baz"));
            Assert.IsInstanceOfType(instance.Outputs, typeof(ProjectTreeOutputSnapshot));
        }

        [TestMethod]
        public void Location_property_is_preserved() {
            var metadata = new BuildStateFile();

            metadata.Location = "Abc";

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual("Abc", instance.Location);
        }

        private static T RoundTrip<T>(T artifact) {
            return ProtoDeserialize<T>(ProtoSerialize(artifact));
        }

        private static T ProtoDeserialize<T>(byte[] data) {
            using (var stream = new MemoryStream(data)) {
                return Serializer.Deserialize<T>(stream);
            }
        }

        private static byte[] ProtoSerialize<T>(T graph) {
            using (var stream = new MemoryStream()) {
                Serializer.Serialize(stream, graph);
                stream.Position = 0;
                return stream.ToArray();
            }
        }
    }

}
