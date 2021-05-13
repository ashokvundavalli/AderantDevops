using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class VisualStudioLocationHelperTests {
        [TestMethod]
        public void GetInstances_can_find_visual_studio() {
            var visualStudioInstances = VisualStudioConfiguration.VisualStudioLocationHelper.GetInstances();

            Assert.IsNotNull(visualStudioInstances);
            Assert.AreNotEqual(0, visualStudioInstances.Count);
        }
    }
}