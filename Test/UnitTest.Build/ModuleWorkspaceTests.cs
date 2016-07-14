using Aderant.Build;
using Aderant.Build.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ModuleWorkspaceTests {
        [TestMethod]
        public void Can_create_via_MEF() {
            var sourceControl = ServiceLocator.GetInstance<ITeamFoundationWorkspace>();
        }
    }
}