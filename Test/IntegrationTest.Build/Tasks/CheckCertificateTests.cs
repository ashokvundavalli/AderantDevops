using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class CheckCertificateTests : BuildTaskTestBase {
        [TestMethod]
        public void CheckCertificateTest() {
            RunTarget("CheckCertificate");
        }
    }
}