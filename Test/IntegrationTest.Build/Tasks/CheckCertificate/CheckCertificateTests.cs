using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.CheckCertificate {
    [TestClass]
    public class CheckCertificateTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void CheckCertificateTest() {
            RunTarget("CheckCertificate");
        }
    }
}