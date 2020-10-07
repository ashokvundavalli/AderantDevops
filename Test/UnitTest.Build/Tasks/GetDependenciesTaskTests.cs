using System.Xml.Linq;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GetDependenciesTaskTests {

        [TestMethod]
        public void Task_item_property_sets_ConfigurationXml_property() {
            var gd = new GetDependencies {
                EnabledResolvers = new string[] {
                    "abc", "def"
                }
            };

            Assert.IsNotNull(gd.ConfigurationXml);
            Assert.AreEqual(@"<BranchConfig>
  <DependencyResolvers>
    <abc />
    <def />
  </DependencyResolvers>
</BranchConfig>",
                gd.ConfigurationXml.ToString());
        }

        [TestMethod]
        public void ReplicationDisabledByDefault() {
            var gd = new GetDependencies();

            gd.ConfigureReplication();

            Assert.IsFalse(gd.EnableReplication);
        }

        [TestMethod]
        public void ReplicationEnabledByDefaultWithProductManifest() {
            var gd = new GetDependencies {
                ProductManifest = "Test"
            };

            gd.ConfigureReplication();

            Assert.IsFalse(gd.EnableReplication);
        }

        [TestMethod]
        public void ReplicationConfiguredtrueViaBranchConfig() {
            var gd = new GetDependencies();

            const string branchConfig = @"<BranchConfig>
  <DependencyReplicationEnabled>true</DependencyReplicationEnabled>
</BranchConfig>";

            gd.ConfigurationXml = XDocument.Parse(branchConfig);

            gd.ConfigureReplication();
            Assert.IsTrue(gd.EnableReplication);
        }

        [TestMethod]
        public void ReplicationConfiguredFalseViaBranchConfig() {
            var gd = new GetDependencies();

            const string branchConfig = @"<BranchConfig>
  <DependencyReplicationEnabled>false</DependencyReplicationEnabled>
</BranchConfig>";

            gd.ConfigurationXml = XDocument.Parse(branchConfig);

            gd.ConfigureReplication();
            Assert.IsFalse(gd.EnableReplication);
        }
    }
}