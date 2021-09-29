using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class VisualStudioLocationHelperTests {
        [TestMethod]
        public void GetInstances_can_find_visual_studio() {
            var visualStudioInstances = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances();

            Assert.IsNotNull(visualStudioInstances);
            Assert.AreNotEqual(0, visualStudioInstances.Count());
        }
    }
}