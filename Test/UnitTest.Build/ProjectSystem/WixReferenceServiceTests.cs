using Aderant.Build.ProjectSystem.References.Wix;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class WixReferenceServiceTests {
        [TestMethod]
        public void TryGetOutputAssemblyWithExtension_returns_windows_installer_extension() {
            string name;
            bool succeed = WixReferenceService.TryGetOutputAssemblyWithExtension(WixReferenceService.PackageType, "foo", out name);

            Assert.IsTrue(succeed);
            Assert.AreEqual("foo.msi", name);
        }
    }

}