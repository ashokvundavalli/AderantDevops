using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class ContextTaskBaseTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void CheckCertificateTest() {
            RunTarget("CheckCertificate");
        }
    }
}