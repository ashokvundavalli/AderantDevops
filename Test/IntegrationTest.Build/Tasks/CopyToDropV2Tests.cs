using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Aderant.Build.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    public class CopyToDropV2Tests : BuildTaskTestBase {
        [TestInitialize]
        public void TestInitialize() {
            tempDropRoot = Path.Combine(TestContext.DeploymentDirectory, MethodBase.GetCurrentMethod().DeclaringType.Name);
            Random random = new Random();
            buildId = string.Format("{0:D5}", random.Next(0, 10000));
        }

        [ClassCleanup]
        public static void ClassCleanup() {
            try {
                Directory.Delete(tempDropRoot, true);
            } catch {
                // Ignored
            }
        }

        [TestCleanup]
        public void TestCleanup() {
            try {
                Directory.Delete(Path.Combine(dropRoot, ModuleName), true);
            } catch {
                // Ignored
            }
        }

        private static string tempDropRoot;
        private static string dropRoot;
        private const string ModuleName = "TestModule";
        private static string buildId;

        [TestMethod]
        public void CopyToDropMasterBuild() {
            dropRoot = Path.Combine(tempDropRoot, MethodBase.GetCurrentMethod().Name);
            const string origin = @"refs\heads\master";
            const string outputOrigin = "master";

            RunTarget(
                "CopyToDropV2",
                new Dictionary<string, string> {
                    { "ModuleName", ModuleName },
                    { "DropRoot", dropRoot },
                    { "Origin", origin },
                    { "BuildNumber", buildId }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "default", "unstable", outputOrigin, buildId);

            Assert.IsTrue(Directory.Exists(outputDirectory));
            Assert.IsTrue(Directory.Exists(Path.Combine(outputDirectory, "Module")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Module", "Product.dll")));
            Assert.IsTrue(Directory.Exists(Path.Combine(outputDirectory, "Test")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Test", "Test.dll")));
        }

        [TestMethod]
        public void CheckDependenciesAreFiltered() {
            dropRoot = Path.Combine(tempDropRoot, MethodBase.GetCurrentMethod().Name);
            const string origin = @"refs\heads\master";
            const string outputOrigin = "master";

            RunTarget(
                "CopyToDropV2",
                new Dictionary<string, string> {
                    { "ModuleName", ModuleName },
                    { "DropRoot", dropRoot },
                    { "Origin", origin },
                    { "BuildNumber", buildId }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "default", "unstable", outputOrigin, buildId);

            Assert.IsTrue(Directory.Exists(Path.Combine(outputDirectory, "Module")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "Module", "Packages.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "Module", "Dependencies.dll")));
        }

        [TestMethod]
        public void CheckDirectoriesAreFiltered() {
            dropRoot = Path.Combine(tempDropRoot, MethodBase.GetCurrentMethod().Name);
            const string origin = @"refs\heads\master";
            const string outputOrigin = "master";

            RunTarget(
                "CopyToDropV2",
                new Dictionary<string, string> {
                    { "ModuleName", ModuleName },
                    { "DropRoot", dropRoot },
                    { "Origin", origin },
                    { "BuildNumber", buildId }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "default", "unstable", outputOrigin, buildId);

            Assert.IsTrue(Directory.Exists(outputDirectory));
            Assert.IsFalse(Directory.Exists(Path.Combine(outputDirectory, "Fluff")));
        }

        [TestMethod]
        public void CopyToDropTfvcBuild() {
            dropRoot = Path.Combine(tempDropRoot, MethodBase.GetCurrentMethod().Name);
            const string origin = @"dev\vnext";
            const string outputOrigin = "dev.vnext";

            RunTarget(
                "CopyToDropV2",
                new Dictionary<string, string> {
                    { "ModuleName", ModuleName },
                    { "DropRoot", dropRoot },
                    { "Origin", origin },
                    { "BuildNumber", buildId }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "default", "unstable", outputOrigin, buildId);

            Assert.IsTrue(Directory.Exists(outputDirectory));
        }

        [TestMethod]
        public void CopyToDropPRBuild() {
            dropRoot = Path.Combine(tempDropRoot, MethodBase.GetCurrentMethod().Name);
            const string origin = @"refs\pull\10600\merge";
            const string outputOrigin = "10600";

            RunTarget(
                "CopyToDropV2",
                new Dictionary<string, string> {
                    { "ModuleName", ModuleName },
                    { "DropRoot", dropRoot },
                    { "Origin", origin },
                    { "BuildNumber", buildId }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "default", "pull", outputOrigin, buildId);

            Assert.IsTrue(Directory.Exists(outputDirectory));
        }
    }
}