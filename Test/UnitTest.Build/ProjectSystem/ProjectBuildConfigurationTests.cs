using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class ProjectBuildConfigurationTests {

        [TestMethod]
        public void FormattingTest() {
            Assert.AreEqual("Release|AnyCPU", ProjectBuildConfiguration.ReleaseOnAnyCpu.ToString());
            Assert.AreEqual("Debug|AnyCPU", ProjectBuildConfiguration.DebugOnAnyCpu.ToString());
        }
    }
}