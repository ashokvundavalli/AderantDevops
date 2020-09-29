using System.Linq;
using System.Reflection;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GetAssemblyPlatformTests {

        [TestMethod]
        public void DetermineCiStatusNullProperty() {
            Assert.IsFalse(AssemblyInspector.DetermineCiStatus(Assembly.GetExecutingAssembly().CustomAttributes.ToArray()).Item1);
            Assert.IsNull(AssemblyInspector.DetermineCiStatus(Assembly.GetExecutingAssembly().CustomAttributes.ToArray()).Item2);
        }

        [TestMethod]
        public void CheckCIStatusRegularAssemblyTest() {
            Assert.IsFalse(GetAssemblyPlatform.ShouldCheckCIStatus("Aderant.Project"));
        }

        [TestMethod]
        public void CheckCIStatusIntegrationTest() {
            Assert.IsTrue(GetAssemblyPlatform.ShouldCheckCIStatus("IntegrationTest.Project"));
        }

        [TestMethod]
        public void CheckCIStatusHelperAssembly() {
            Assert.IsFalse(GetAssemblyPlatform.ShouldCheckCIStatus("IntegrationTest.Module.Helper"));
        }

        [TestMethod]
        public void CheckCIStatusHelperAssemblyPlural() {
            Assert.IsFalse(GetAssemblyPlatform.ShouldCheckCIStatus("IntegrationTest.Module.Helpers"));
        }
    }
}
