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

            var workflow = new ResolverWorkflow(NullLogger.Default);
            workflow.ConfigurationXml = XDocument.Parse(xml);

            Assert.IsTrue(workflow.Request.ValidatePackageConstraints);
        }
    }
}