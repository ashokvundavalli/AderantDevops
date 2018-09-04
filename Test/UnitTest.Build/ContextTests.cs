using System;
using System.Collections.Generic;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class BuildOperationContextTests {

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

            context.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { @"obj/baz.dll" }, @"..\..\bin", "obj");

            IDictionary<string, OutputFilesSnapshot> outputs = context.GetProjectOutputs();

            OutputFilesSnapshot outputFilesSnapshot = outputs["Foo"];
            Assert.AreEqual(0, outputFilesSnapshot.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_keeps_output() {
            var context = new BuildOperationContext();

            context.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { "obj/baz.dll", "baz.dll" }, @"..\..\bin", "obj");

            IDictionary<string, OutputFilesSnapshot> outputs = context.GetProjectOutputs();

            OutputFilesSnapshot outputFilesSnapshot = outputs["Foo"];
            Assert.AreEqual(1, outputFilesSnapshot.FilesWritten.Length);
        }

        [TestMethod]
        public void RecordOutputs_is_deterministic() {
            var context = new BuildOperationContext();

            context.RecordProjectOutputs(Guid.NewGuid(), "", "Foo", new[] { "B", "10000", "A", "001", "Z", "1" }, @"..\..\bin", "obj");

            IDictionary<string, OutputFilesSnapshot> outputs = context.GetProjectOutputs();

            OutputFilesSnapshot outputFilesSnapshot = outputs["Foo"];
            Assert.AreEqual("001", outputFilesSnapshot.FilesWritten[0]);
            Assert.AreEqual("1", outputFilesSnapshot.FilesWritten[1]);
            Assert.AreEqual("10000", outputFilesSnapshot.FilesWritten[2]);
            Assert.AreEqual("A", outputFilesSnapshot.FilesWritten[3]);
            Assert.AreEqual("B", outputFilesSnapshot.FilesWritten[4]);
            Assert.AreEqual("Z", outputFilesSnapshot.FilesWritten[5]);
        }
    }

}
