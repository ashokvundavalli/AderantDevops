using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.CheckCertificate {
    [TestClass]
    public class CheckCertificateTests : BuildTaskTestBase {
        [TestMethod]
        public void CheckCertificateTest() {
            RunTarget("CheckCertificate");
        }
    }
}