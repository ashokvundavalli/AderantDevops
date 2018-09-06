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
    }

}
