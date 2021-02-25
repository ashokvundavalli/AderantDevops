using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {

    [TestClass]
    [DeploymentItem(MSBuildIntegrationTestBase.TestDeployment)]
    public class AssemblyInitializer {

        public TestContext TestContext { get; set; }

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context) {
            Environment.CurrentDirectory = context.DeploymentDirectory;

            var nativeAssemblyDirectories = new[] {
                Path.Combine(context.DeploymentDirectory, "lib", "win32"),
                Path.Combine(context.DeploymentDirectory, MSBuildIntegrationTestBase.TestDeployment, "lib", "win32"),

            };

            foreach (var directory in nativeAssemblyDirectories) {
                if (Directory.Exists(directory)) {
                    LibGit2Sharp.GlobalSettings.NativeLibraryPath = directory;

                    var nativeLibrary = Path.Combine(directory, Environment.Is64BitProcess ? "x64" : "x86", "git2-106a5f2.dll");
                    if (!File.Exists(nativeLibrary)) {
                        throw new FileNotFoundException("Native library not found: " + nativeLibrary, nativeLibrary);
                    }
                    return;
                }
            }
        }

        [TestMethod]
        public void List_files()
        {
            Directory.EnumerateDirectories(TestContext.DeploymentDirectory).ToList().ForEach(s => TestContext.WriteLine(s));

            Directory.EnumerateDirectories(Path.Combine(TestContext.DeploymentDirectory, "TestDeployment")).ToList().ForEach(s => TestContext.WriteLine(s));
            TestContext.WriteLine("zzzz");
            Directory.EnumerateDirectories(Path.Combine(TestContext.DeploymentDirectory)).ToList().ForEach(s => TestContext.WriteLine(s));
        }

        [TestMethod]
        public void Native_library_path_exists() {
            Assert.IsTrue(Directory.Exists(LibGit2Sharp.GlobalSettings.NativeLibraryPath));
        }
    }
}
