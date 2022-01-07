using System;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    public class AssemblyInitializer : MSBuildIntegrationTestBase {
        private static bool assemblyInitializeFailed;

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context) {
            ProjectSequencer.GiveTimeToReviewTree = false;

            Environment.CurrentDirectory = context.DeploymentDirectory;

            var nativeAssemblyDirectories = new[] {
                Path.Combine(context.DeploymentDirectory, "Build.Tools", "lib", "win32"),
            };

            foreach (var directory in nativeAssemblyDirectories) {
                if (Directory.Exists(directory)) {
                    var nativeLibrary = Path.Combine(directory, Environment.Is64BitProcess ? "x64" : "x86", "git2-b7bad55.dll");

                    if (!File.Exists(nativeLibrary)) {
                        throw new FileNotFoundException("Native library not found: " + nativeLibrary, nativeLibrary);
                    }

                    LibGit2Sharp.GlobalSettings.NativeLibraryPath = Path.GetDirectoryName(nativeLibrary);
                    return;
                }

                context.WriteLine($"Directory {directory} does not exist.");
            }

            assemblyInitializeFailed = true;

            // Dump out the test deployment directory on failure
            context.WriteLine("Could not find the native git binaries. The contents of the deployment directory is...");
            Directory.EnumerateDirectories(context.DeploymentDirectory, "*", SearchOption.AllDirectories).ToList().ForEach(s => context.WriteLine(s));

            if (assemblyInitializeFailed) {
                throw new SystemException("Unable to find git library!");
            }
        }

        [TestMethod]
        public void Native_library_path_exists() {
            Assert.IsFalse(assemblyInitializeFailed, "Assembly Initialize failed");

            Assert.IsTrue(Directory.Exists(LibGit2Sharp.GlobalSettings.NativeLibraryPath));
        }

    }
}