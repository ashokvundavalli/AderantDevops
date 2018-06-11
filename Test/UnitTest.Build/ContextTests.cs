using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ContextTests {
        [TestMethod]
        public void IsDesktopBuildReturnsCorrectValueTest() {
            Context context = new Context(new BuildMetadata());

            Assert.IsTrue(context.IsDesktopBuild);

            context.BuildMetadata.HostEnvironment = HostEnvironment.Vsts;

            Assert.IsFalse(context.IsDesktopBuild);
        }
    }
}
