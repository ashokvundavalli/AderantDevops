using System;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class ResolverWorkflowTests {
        [TestMethod]
        public void ConfigurationXml_parsing() {
            string xml = @"<DependencyResolvers>
    <NupkgResolver>
      <ValidatePackageConstraints>true</ValidatePackageConstraints>
    </NupkgResolver>
  </DependencyResolvers>";

            var workflow = new ResolverWorkflow(NullLogger.Default, null);
            workflow.WithConfiguration(XDocument.Parse(xml));

            Assert.IsTrue(workflow.GetCurrentRequest().ValidatePackageConstraints);
        }

        [TestMethod]
        public void Task_item_property_sets_ConfigurationXml_property() {
            var enabledResolvers = new[] {
                "abc", "def"
            };

            var workflow = new ResolverWorkflow(NullLogger.Default, null);
            workflow.WithResolvers(enabledResolvers);

            Assert.IsTrue(enabledResolvers.SequenceEqual(workflow.EnabledResolvers));
        }

        [TestMethod]
        public void ReplicationConfiguredtrueViaBranchConfig() {
            string branchConfig = @"<BranchConfig>
          <DependencyReplicationEnabled>true</DependencyReplicationEnabled>
        </BranchConfig>";

            var workflow = new ResolverWorkflow(NullLogger.Default, null);
            workflow.WithConfiguration(XDocument.Parse(branchConfig));

            Assert.IsFalse(workflow.GetCurrentRequest().ReplicationExplicitlyDisabled);
        }

        [TestMethod]
        public void ReplicationConfiguredFalseViaBranchConfig() {
            string branchConfig = @"<BranchConfig>
          <DependencyReplicationEnabled>false</DependencyReplicationEnabled>
        </BranchConfig>";

            var workflow = new ResolverWorkflow(NullLogger.Default, null);
            workflow.WithConfiguration(XDocument.Parse(branchConfig));

            Assert.IsTrue(workflow.GetCurrentRequest().ReplicationExplicitlyDisabled);
        }
    }
}
