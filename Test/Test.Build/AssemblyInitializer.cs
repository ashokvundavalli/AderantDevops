using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    public class AssemblyInitializer {

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context) {
            GlobalSettings.NativeLibraryPath = context.DeploymentDirectory;
        }

    }
}
