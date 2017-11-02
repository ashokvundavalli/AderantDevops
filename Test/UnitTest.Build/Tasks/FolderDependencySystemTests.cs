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
            const string origin = @"refs\heads\CopyToDropv2";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            Assert.AreEqual(quality, "unstable");
        }

        [TestMethod]
        public void GetQualityPRTest() {
            const string origin = @"refs\pull\10600\merge";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            Assert.AreEqual(quality, "pull");
        }

        [TestMethod]
        public void GetBuildDropPathMasterTest() {
            const string origin = @"refs\heads\CopyToDropv2";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            string buildDropPath = FolderDependencySystem.BuildDropPath(ModuleName, Component, quality, origin, buildId);

            string outputDirectory = Path.Combine(ModuleName, Component, "unstable", "CopyToDropv2", buildId);

            Assert.AreEqual(buildDropPath, outputDirectory);
        }

        [TestMethod]
        public void GetBuildDropPathPRTest() {
            const string origin = @"refs\pull\10600\merge";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            string buildDropPath = FolderDependencySystem.BuildDropPath(ModuleName, Component, quality, origin, buildId);

            string outputDirectory = Path.Combine(ModuleName, Component, "pull", "10600", buildId);

            Assert.AreEqual(outputDirectory, buildDropPath);
        }

        [TestMethod]
        public void GetBuildDropPathTfvcTest() {
            const string origin = @"dev\vnext";
            string quality = FolderDependencySystem.GetQualityMoniker(origin);
            string buildDropPath = FolderDependencySystem.BuildDropPath(ModuleName, Component, quality, origin, buildId);

            string outputDirectory = Path.Combine(ModuleName, Component, "unstable", "dev.vnext", buildId);

            Assert.AreEqual(buildDropPath, outputDirectory);
        }

        [TestMethod]
        public void RemovePrefixMasterTest() {
            string prefix = @"refs\heads\";
            const string origin = @"refs\heads\Test";
            string resultOrigin = FolderDependencySystem.RemovePrefix(origin, prefix);
            Assert.AreEqual(resultOrigin, "Test");
        }

        [TestMethod]
        public void RemovePrefixPRTest() {
            string prefix = @"refs\heads\";
            const string origin = @"refs\pull\10600\merge";
            string resultOrigin = FolderDependencySystem.RemovePrefix(origin, prefix);
            Assert.AreEqual(resultOrigin, @"pull\10600\merge");
        }
    }
}
