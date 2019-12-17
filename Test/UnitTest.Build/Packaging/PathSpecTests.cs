using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class PathSpecTests {

        [TestMethod]
        public void Equality() {
            var a = new PathSpec("A", "");
            var b = new PathSpec("A", "");

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
        }
    }
}