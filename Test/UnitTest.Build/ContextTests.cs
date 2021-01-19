using System;
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
        [ExpectedException(typeof(ArgumentException))]
        public void Root_cannot_be_unset() {
            var ctx = new BuildOperationContext();

            ctx.BuildRoot = "abc";
            ctx.BuildRoot = null;
        }
    }

}