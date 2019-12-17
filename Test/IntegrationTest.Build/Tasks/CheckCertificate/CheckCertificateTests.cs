using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.CheckCertificate {
    [TestClass]
    public class CheckCertificateTests : MSBuildIntegrationTestBase {
        [TestMethod]
        public void CheckCertificateTest() {
            // Dummy reference to mark the code as used.
            Type type = typeof(Aderant.Build.Tasks.CheckCertificate);

            RunTarget("CheckCertificate");
        }
    }
}