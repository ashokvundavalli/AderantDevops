using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GetDependenciesTaskTests {

        [TestMethod]
        public void Task_item_property_sets_ConfigurationXml_property() {
            var gd = new GetDependencies();
            gd.EnabledResolvers = new string[] { "abc", "def" };

            Assert.IsNotNull(gd.ConfigurationXml);
            Assert.AreEqual(
                @"<BranchConfig>
  <DependencyResolvers>
    <abc />
    <def />
  </DependencyResolvers>
</BranchConfig>",
                gd.ConfigurationXml);
        }
    }
}