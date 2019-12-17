using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Build.DependencyAnalyzer;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class OnDiskProjectInfoTests {

        [TestMethod]
        public void SetPropertiesNeededForTracking_adds_metadata() {
            var item = new ItemGroupItem("");
            OnDiskProjectInfo.SetPropertiesNeededForTracking(item, new TestConfiguredProject(null));

            Assert.AreNotEqual(0, item.MetadataKeys);
        }
    }
}
