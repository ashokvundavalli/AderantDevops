using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class AssemblyInitializer {

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context) {
            ProjectSequencer.GiveTimeToReviewTree = false;
        }
    }
}