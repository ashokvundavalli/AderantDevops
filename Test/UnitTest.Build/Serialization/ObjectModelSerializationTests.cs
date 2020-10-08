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
using UnitTest.Build.Serialization;

namespace UnitTest.Build {
    [TestClass]
    public class ObjectModelSerializationTests : SerializationBase {

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
            metadata.BuildRoot = "abc";

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, instance.WrittenStateFiles.Count);
            Assert.AreEqual("abc", instance.BuildRoot);
            Assert.IsFalse(instance.IsDesktopBuild);
        }

        [TestMethod]
        public void Variables_from_PutVariable_can_survive_round_trip() {
            var metadata = new BuildOperationContext();
            metadata.PutVariable("", "abc", "def");

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual("def", instance.Variables["abc"]);
        }

        [TestMethod]
        public void Variables_are_returned_from_GetVariable_can_survive_round_trip() {
            var metadata = new BuildOperationContext();
            metadata.PutVariable("", "abc", "def");

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);

            string value = instance.GetVariable("", "abc");

            Assert.AreEqual("def", value);
            Assert.AreEqual("def", instance.Variables["abc"]);
        }

        [TestMethod]
        public void Scoped_variables_can_be_survive_round_trip() {
            var metadata = new BuildOperationContext();
            metadata.PutVariable("1", "abc", "def");

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, metadata.ScopedVariables["1"].Keys.Count);
        }

        [TestMethod]
        public void Variables_can_be_survive_round_trip() {
            var metadata = new BuildOperationContext();
            metadata.Variables["abc"] = "def";

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual("def", instance.Variables["abc"]);
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

        [TestMethod]
        public void BuildDirectoryContribution() {
            var metadata = new BuildDirectoryContribution("Abc");

            var instance = RoundTrip(metadata);

            Assert.IsNotNull(instance);
            Assert.AreEqual("Abc", instance.File);
        }
    }
}
