using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class FileRestoreTests {
        [TestMethod]
        public void RestoreWithMatchingOutputPath() {
            string restorePath = FileRestore.CalculateRestorePath(@"..\..\Bin\Module\Fancy.Assembly.dll", @"..\..\Bin\Module\");

            Assert.AreEqual("Fancy.Assembly.dll", restorePath);
        }

        [TestMethod]
        public void RestoreWithKnownOutputPath() {
            string restorePath = FileRestore.CalculateRestorePath(@"..\..\Bin\Module\Web.Fancy.zip", @"bin\");

            Assert.AreEqual("Web.Fancy.zip", restorePath);
        }
    }
}
