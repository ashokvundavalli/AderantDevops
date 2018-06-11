using System;
using System.IO;
using Aderant.Build;
using Aderant.Build.Tasks.BuildTime.Sequencer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.BuildTime {
    [TestClass]
    public class BuildSequencerTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ContextValidationFailureTest() {
            BuildSequencer buildSequencer = new BuildSequencer(null, new Context(null), null, null, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ContextValidationFailureTest() {
            BuildSequencer buildSequencer = new BuildSequencer(null, new Context(null), null, null, null);
        }

        [TestMethod]
        public void ContextValidationTest() {
            BuildSequencer buildSequencer = new BuildSequencer(null, new Context(null) { BuildRoot = new DirectoryInfo(TestContext.DeploymentDirectory) }, null, null, null);
        }
    }
}
