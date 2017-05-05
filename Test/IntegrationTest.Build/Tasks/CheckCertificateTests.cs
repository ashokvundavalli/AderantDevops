using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("IntegrationTest.targets")]
    [DeploymentItem("Aderant.Build.Common.targets")]
    public class CheckCertificateTests : BuildTaskTestBase {
        [TestMethod]
        public void CheckCertificateTest() {
            RunTarget("CheckCertificate");
        }
    }
}