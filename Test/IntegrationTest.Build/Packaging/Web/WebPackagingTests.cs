using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Packaging.Web {
    [TestClass]
    [DeploymentItem("Web.Site1", "Web.Site1")]
    public class WebPackagingTests : MSBuildIntegrationTestBase {

        [TestMethod]
        public void CollectSharedProjectItems_returns_all_items_when_not_filtering() {
            RunTarget("PackageWeb", new Dictionary<string, string> { { "UseProjectNameWhenCollectingSharedContent", "false" } });

            TargetResult targetResult = Result.ResultsByTarget["PackageWeb"];

            var items = targetResult.Items;

            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Length);
        }

        [TestMethod]
        public void CollectSharedProjectItems_returns_subset_items_when_filtering() {
            RunTarget("PackageWeb", new Dictionary<string, string> {
                { "UseProjectNameWhenCollectingSharedContent", "true" },
                { "WebProjectName", "Web.Site1" }
            });

            TargetResult targetResult = Result.ResultsByTarget["PackageWeb"];

            var items = targetResult.Items;

            Assert.IsNotNull(items);
            Assert.AreEqual(1, items.Length);
            Assert.IsTrue(items[0].MetadataNames.OfType<string>().Contains("Link"));
            Assert.IsTrue(items[0].MetadataNames.OfType<string>().Contains("Type"));
        }
    }
}