using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Build.DependencyAnalyzer;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class TrackedProjectTests {

        [TestMethod]
        public void SetPropertiesNeededForTracking_adds_metadata() {
            var item = new ItemGroupItem("");
            TrackedProject.SetPropertiesNeededForTracking(item, new TestConfiguredProject(null));

            Assert.AreNotEqual(0, item.MetadataKeys);
        }
    }
}
