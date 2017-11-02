using System;
using System.IO;
using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class FolderDependencySystemTests {

        [TestInitialize]
        public void TestInitialize() {
            Random random = new Random();
            buildId = string.Format("{0:D5}", random.Next(0, 10000));
        }

        private const string ModuleName = "TestModule";
        private const string Component = "default";
        private static string buildId;

        [TestMethod]
        public void GetQualityMasterTest() {
            const string origin = "refs/heads/CopyToDropv2";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            Assert.AreEqual("unstable", quality);
        }

        [TestMethod]
        public void GetQualityPRTest() {
            const string origin = "refs/pull/10600/merge";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            Assert.AreEqual("pull", quality);
        }

        [TestMethod]
        public void GetBuildDropPathMasterTest() {
            const string origin = "refs/heads/CopyToDropv2";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            string buildDropPath = FolderDependencySystem.BuildDropPath(ModuleName, quality, origin, buildId, Component);

            string outputDirectory = Path.Combine(ModuleName, "unstable", "CopyToDropv2", buildId, Component);

            Assert.AreEqual(outputDirectory, buildDropPath);
        }

        [TestMethod]
        public void GetBuildDropPathPRTest() {
            const string origin = "refs/pull/10600/merge";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            string buildDropPath = FolderDependencySystem.BuildDropPath(ModuleName, quality, origin, buildId, Component);

            string outputDirectory = Path.Combine(ModuleName, "pull", "10600", buildId, Component);

            Assert.AreEqual(outputDirectory, buildDropPath);
        }

        [TestMethod]
        public void GetBuildDropPathTfvcTest() {
            const string origin = "dev/vnext";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            string buildDropPath = FolderDependencySystem.BuildDropPath(ModuleName, quality, origin, buildId, Component);

            string outputDirectory = Path.Combine(ModuleName, "unstable", "dev.vnext", buildId, Component);

            Assert.AreEqual(outputDirectory, buildDropPath);
        }

        [TestMethod]
        public void RemovePrefixMasterTest() {
            string prefix = "refs/heads/";
            const string origin = "refs/heads/Test";
            string resultOrigin = FolderDependencySystem.RemovePrefix(origin, prefix);
            Assert.AreEqual("Test", resultOrigin);
        }

        [TestMethod]
        public void RemovePrefixPRTest() {
            string prefix = "refs\\heads\\";
            const string origin = "refs\\pull\\10600\\merge";
            var resultOrigin = FolderDependencySystem.RemovePrefix(origin, prefix);
            Assert.AreEqual(@"pull\10600\merge", resultOrigin);
        }
    }
}
