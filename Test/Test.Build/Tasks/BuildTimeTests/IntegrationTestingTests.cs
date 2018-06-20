using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.BuildTimeTests {
    [TestClass]
    public class IntegrationTestingTests : BuildTaskTestBase {
        private string moduleBuildTempDirectory;
        private static readonly string time = "2018";
        private readonly string databaseName = string.Concat("Expert_", time);
        private readonly string environmentName = string.Concat("Local_", time);
        private const string ModuleName = "ServerImage";
        private const string isImportPackage = "true";
        private const string CommonPackages = "CommonApplications.zip";

        [TestInitialize]
        public void TestInitialize() {
            string testContextModuleDirectory = Path.Combine(TestContext.DeploymentDirectory, $@"Resources\{ModuleName}");
            moduleBuildTempDirectory = Path.Combine(@"C:\Temp", ModuleName);

            foreach (string dirPath in Directory.GetDirectories(testContextModuleDirectory, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(dirPath.Replace(testContextModuleDirectory, moduleBuildTempDirectory));
            }

            foreach (string newPath in Directory.GetFiles(testContextModuleDirectory, "*.*", SearchOption.AllDirectories)) {
                File.Copy(newPath, newPath.Replace(testContextModuleDirectory, moduleBuildTempDirectory), true);
            }

            Directory.CreateDirectory(Path.Combine(moduleBuildTempDirectory, ".git"));
        }

        [TestCleanup]
        public void TestCleanup() {
            try {
                //LightsOff();
            } catch {
                // ignored
            }

            try {
                //Directory.Delete(Directory.GetParent(moduleBuildTempDirectory).FullName, true);
            } catch {
                // ignored
            }
        }

        [TestMethod]
        [Ignore]
        public void GetServerImageTest() {
            GetServerImage();
        }

        [TestMethod]
        [Ignore]
        public void ProvisionDatabaseTest() {
            GetServerImage();
            ProvisionDatabase();
            LightsOff();
        }

        [TestMethod]
        [Ignore]
        public void PackageImportTest() {
            GetServerImage();
            ProvisionDatabase();
            LightUp();
            ExecuteQueryViewsScript();
            PackageImport();
            LightsOff();
        }

        [TestMethod]
        [Ignore]
        public void LightUpTest() {
            GetServerImage();
            ProvisionDatabase();
            LightUp();
            ExecuteQueryViewsScript();
            LightsOff();
        }

        /// <summary>
        /// Test if UpdateServerImage works. Refer to Resources\Framework\Bin\Module\fake.role.xml.
        /// </summary>
        [TestMethod]
        [Ignore]
        public void UpdateServerImageTest() {
            GetServerImage();
            UpdateServerImage();

            var serverImageLocation = Path.Combine(moduleBuildTempDirectory, "Dependencies", environmentName);
            var files = Directory.GetFiles(serverImageLocation, "Dependencies.dll", SearchOption.AllDirectories);
            Assert.AreEqual(1, files.Length);
            Assert.IsTrue(files.First().EndsWith("\\Services\\Dependencies.dll", StringComparison.OrdinalIgnoreCase));

            files = Directory.GetFiles(serverImageLocation, "Packages.dll", SearchOption.AllDirectories);
            Assert.AreEqual(1, files.Length);
            Assert.IsTrue(files.First().EndsWith("\\Services\\FrameworkServices\\Packages.dll", StringComparison.OrdinalIgnoreCase));

            files = Directory.GetFiles(serverImageLocation, "Product.dll", SearchOption.AllDirectories);
            Assert.AreEqual(0, files.Length);
        }

        [TestMethod]
        [Ignore]
        public void LightsOffTest() {
            GetServerImage();
            LightUp();
            LightsOff();
        }

        [TestMethod]
        [Ignore]
        public void TestLightUpAndImportPackage() {
            GetServerImage();
            ProvisionDatabase();
            UpdateServerImage();
            LightUp();
            ExecuteQueryViewsScript();
            PackageImport();
        }

        [TestMethod]
        [Ignore]
        public void UpdateManifestTest() {
            UpdateManifest();
        }

        private void UpdateManifest() {
            RunTarget(
                "UpdateManifest",
                new Dictionary<string, string> {
                    { "PackageDependency", "Aderant.Disbursements|GetAction:NuGet|Version:>= 0 build§Item2|AssemblyVersion:1.8.0.0" }
                });
        }

        private void UpdateServerImage() {
            RunTarget(
                "UpdateServerImage",
                new Dictionary<string, string> {
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory },
                    { "ImageName", environmentName }
                });
        }

        private void ProvisionDatabase() {
            RunTarget(
                "ProvisionExpertDatabase",
                new Dictionary<string, string> {
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory },
                    { "DatabaseName", databaseName }
                });
        }

        private void PackageImport() {
            RunTarget(
                "PackageImport",
                new Dictionary<string, string> {
                    { "ImportPackages", isImportPackage },
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory },
                    { "ImageName", environmentName },
                    { "CommonPackageNames", CommonPackages }
                });
        }

        private void GetServerImage() {
            RunTarget(
                "GetServerImage",
                new Dictionary<string, string> {
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory }
                });
        }

        private void LightUp() {
            RunTarget(
                "LightUpServerImage",
                new Dictionary<string, string> {
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory },
                    { "DatabaseName", databaseName },
                    { "ImageName", environmentName }
                });
        }

        private void ExecuteQueryViewsScript() {
            RunTarget(
                "ExecuteQueryViewsScript",
                new Dictionary<string, string> {
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory },
                    { "DatabaseName", databaseName },
                    { "ExpertEnvironmentName", environmentName }
                });
        }

        private void RunIntegrationTests() {
            RunTarget(
                "RunIntegrationTests",
                new Dictionary<string, string> {
                    { "SolutionDirectoryPath", @"C:\Git\Framework\" }, // Requires a module with some tests to run.
                    { "ExpertEnvironmentUrl", $"http://localhost/expert_{environmentName}"}
                });
        }

        private void LightsOff() {
            RunTarget(
                "LightsOff",
                new Dictionary<string, string> {
                    { "ModuleBuildTempDirectory", moduleBuildTempDirectory },
                    { "ImageName", environmentName },
                    { "DatabaseName", databaseName }
                });
        }
    }
}