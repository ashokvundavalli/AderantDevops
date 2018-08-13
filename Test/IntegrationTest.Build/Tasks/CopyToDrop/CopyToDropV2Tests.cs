using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.CopyToDrop {
    [TestClass]
    public class CopyToDropV2Tests : MSBuildIntegrationTestBase {
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

            try {
                File.Delete(Path.Combine(buildInfrastructureDirectory, $@"Test\IntegrationTest.Build\Resources\{ModuleName}\CopyToDrop.ps1"));
            } catch {
                // Ignored
            }

        }

        private static string tempDropRoot;
        private static string dropRoot;
        private const string ModuleName = "Framework";
        private static readonly string buildInfrastructureDirectory = Directory.GetParent(Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName).FullName).FullName;
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
                    { "BuildNumber", buildId },
                    { "GenerateFile", "$false" }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "unstable", outputOrigin, buildId, "default");

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
                    { "BuildNumber", buildId },
                    { "GenerateFile", "$false" }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "unstable", outputOrigin, buildId, "default");

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
                    { "BuildNumber", buildId },
                    { "GenerateFile", "$false" }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "unstable", outputOrigin, buildId, "default");

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
                    { "BuildNumber", buildId },
                    { "GenerateFile", "$false" }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "unstable", outputOrigin, buildId, "default");

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
                    { "BuildNumber", buildId },
                    { "GenerateFile", "$false" }
                });

            string outputDirectory = Path.Combine(dropRoot, ModuleName, "pull", outputOrigin, buildId, "default");

            Assert.IsTrue(Directory.Exists(outputDirectory));
        }

        [TestMethod]
        public void TestVnextScriptGeneration() {
            dropRoot = Path.Combine(tempDropRoot, MethodBase.GetCurrentMethod().Name);
            const string origin = @"refs\pull\10600\merge";

            RunTarget(
                "CopyToDropV2",
                new Dictionary<string, string> {
                    { "ModuleName", ModuleName },
                    { "DropRoot", dropRoot },
                    { "Origin", origin },
                    { "BuildNumber", buildId },
                    { "GenerateFile", "$true" }
                });

            Assert.IsTrue(File.Exists($@"{buildInfrastructureDirectory}\Test\IntegrationTest.Build\Resources\{ModuleName}\CopyToDrop.ps1"));
        }
    }
}