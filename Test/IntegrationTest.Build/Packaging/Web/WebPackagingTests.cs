using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Packaging.Web {
    [TestClass]
    [DeploymentItem("Packaging\\Web.Site1", "Web.Site1")]
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
        public void _Layout_cshtml_is_not_collected() {
            RunTarget("PackageWeb", new Dictionary<string, string> { { "UseProjectNameWhenCollectingSharedContent", "false" } });

            TargetResult targetResult = Result.ResultsByTarget["PackageWeb"];

            var items = targetResult.Items;

            Assert.IsTrue(items.All(s => s.GetMetadata("Filename") != "_Layout.cshtml"));
        }

        [TestMethod]
        public void CollectSharedProjectItems_returns_subset_items_when_filtering() {
            RunTarget(
                "PackageWeb",
                new Dictionary<string, string> {
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

        [TestMethod]
        public void CollectSharedProjectItems_returns_subset_items_when_filtering2() {
            RunTarget(
                "WriteContentFileXml",
                new Dictionary<string, string> {
                    { "UseProjectNameWhenCollectingSharedContent", "true" },
                    { "WebProjectName", "Web.Site1" },

                    // Tell the task we want ManualLogOn
                    { "IncludeAuthenticationFiles", "true" },

                    // Tell the task we want XML back
                    { "OutputSharedContentFileXml", "true" },

                    // Tell the target we want to run it
                    { "OutputSharedWebContentFile", "true" }
                });

            TargetResult targetResult = Result.ResultsByTarget["WriteContentFileXml"];
            XElement element = XElement.Parse(targetResult.Items.Last().ItemSpec);

            Assert.IsTrue(element.Descendants().Any(s => string.Equals(s.Value, "ManualLogOn\\Web.config")));
        }
    }
}