using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class BuildOperationContextTests {

        [TestMethod]
        public void Context_is_serializable() {
            var ctx = new BuildOperationContext();

            var formatter = new BinaryFormatter();
            formatter.Serialize(new MemoryStream(), ctx);
        }

        [TestMethod]
        public void SourceTreeInfo_is_serializable() {
            var ctx = new BuildOperationContext();
            ctx.SourceTreeMetadata = new SourceTreeMetadata {
                BucketIds = new List<BucketId> { new BucketId("", "") }
            };

            var formatter = new BinaryFormatter();
            formatter.Serialize(new MemoryStream(), ctx);
        }

        [TestMethod]
        public void GetService_creates_service_from_type_name() {
            var ctx = new BuildOperationContext();
            var service = ctx.GetService("Aderant.Build.Services.FileSystemService");

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(PhysicalFileSystem));
        }

        [TestMethod]
        public void GetService_creates_service_from_conditional_export() {
            var ctx = new BuildOperationContext();
            var service = ctx.GetService(typeof(IFileSystem2).FullName);

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(PhysicalFileSystem));
        }

        [TestMethod]
        public void BuildOptions_can_be_set() {
            var ctx = new BuildOperationContext();
            BuildSwitches switches = ctx.Switches;

            switches.Downstream = true;

            ctx.Switches = switches;

            Assert.AreEqual(true, ctx.Switches.Downstream);
        }

        [TestMethod]
        public void RecordOutputs_removes_obj_files() {
            var context = new BuildOperationContext();

            context.RecordProjectOutputs("", "Foo", new[] { @"obj/baz.dll" }, @"..\..\bin", "obj");

            IDictionary<string, ProjectOutputs> outputs = context.GetProjectOutputs();

            ProjectOutputs projectOutputs = outputs["Foo"];
            Assert.AreEqual(0, projectOutputs.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_keeps_output() {
            var context = new BuildOperationContext();

            context.RecordProjectOutputs("", "Foo", new[] { "obj/baz.dll", "baz.dll" }, @"..\..\bin", "obj");

            IDictionary<string, ProjectOutputs> outputs = context.GetProjectOutputs();

            ProjectOutputs projectOutputs = outputs["Foo"];
            Assert.AreEqual(1, projectOutputs.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_is_deterministic() {
            var context = new BuildOperationContext();

            context.RecordProjectOutputs("", "Foo", new[] { "B", "10000", "A", "001", "Z", "1" }, @"..\..\bin", "obj");

            IDictionary<string, ProjectOutputs> outputs = context.GetProjectOutputs();

            ProjectOutputs projectOutputs = outputs["Foo"];
            Assert.AreEqual("001", projectOutputs.FilesWritten[0]);
            Assert.AreEqual("1", projectOutputs.FilesWritten[1]);
            Assert.AreEqual("10000", projectOutputs.FilesWritten[2]);
            Assert.AreEqual("A", projectOutputs.FilesWritten[3]);
            Assert.AreEqual("B", projectOutputs.FilesWritten[4]);
            Assert.AreEqual("Z", projectOutputs.FilesWritten[5]);
        }
    }

}
