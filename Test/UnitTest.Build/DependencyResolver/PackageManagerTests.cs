using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Model;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class PackageManagerTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void The_same_name_is_allowed_in_different_groups() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            DependencyGroup items1;
            DependencyGroup items2;
            using (var packageManager = CreatePackageManager(fs)) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("Foo", "Main"),
                        DependencyRequirement.Create("Foo", "Bar"),
                    });

                items1 = packageManager.GetDependencies("Main");
                items2 = packageManager.GetDependencies("Bar");
            }

            Assert.AreEqual(1, items1.Requirements.Count);
            Assert.AreEqual(1, items2.Requirements.Count);
        }

        private PaketPackageManager CreatePackageManager(Mock<IFileSystem2> fs = null, IWellKnownSources source = null) {
            return new PaketPackageManager(
                GetTestDirectoryPath(),
                fs?.Object,
                source ?? new WellKnownPackageSources(),
                new NullLogger());
        }

        [TestMethod]
        public void Azure_package_server_is_listed_first() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            string[] packageManagerLines;
            using (var packageManager = CreatePackageManager(fs, new WellKnownPackageSources.AzureHostedSources())) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("Foo", "Main"),
                        DependencyRequirement.Create("Foo", "Bar"),
                    });

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual("source " + Constants.PackageServerUrlV3, packageManagerLines[0]);
        }

        [TestMethod]
        [ExpectedException(typeof(DependencyException))]
        public void Package_manager_fails_on_constraint_conflicts() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());

            using (var packageManager = CreatePackageManager(fs)) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("Foo", "Main", new VersionRequirement { ConstraintExpression = ">= 1.0.0" }),
                        DependencyRequirement.Create("Foo", "Main", new VersionRequirement { ConstraintExpression = ">= 2.0.0" }),
                    },
                    new ResolverRequest(new NullLogger(), (ExpertModule[])null) {
                        ValidatePackageConstraints = true
                    });
            }

        }

        [TestMethod]
        public void GroupGeneration() {
            string lines = @"
group Test
source test
nuget Gotta.Have.It 4.20 ci";

            using (var packageManager = CreatePackageManager()) {
                packageManager.SetDependenciesFile(lines);

                DependencyGroup dependencyGroup = packageManager.GetDependencies("Test");

                Assert.IsNotNull(dependencyGroup);
            }
        }


        [TestMethod]
        public void DatabaseSourceAddition() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            string[] packageManagerLines;

            var request = new ResolverRequest(NullLogger.Default);
            request.AddModule("C:\\Abc");
            request.AddModule("C:\\Def");

            using (var packageManager = CreatePackageManager(fs, new WellKnownPackageSources.NonAzureHostedSources())) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("Aderant.Database.Backup", "Main"),
                    },
                    request);

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual($"source {Constants.DatabasePackageUri}", packageManagerLines[0]);
        }

        [TestMethod]
        public void When_a_single_module_is_in_the_build_official_nuget_source_allowed() {
            string lines = @"
source https://www.nuget.org/api/v2
references: strict
nuget ThePackageFromNuget";

            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            string[] packageManagerLines;

            var request = new ResolverRequest(NullLogger.Default);
            request.AddModule("C:\\SingleModule");

            using (var packageManager = CreatePackageManager(fs)) {
                packageManager.SetDependenciesFile(lines);

                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("SomeOtherPackage", "Main"),
                    },
                    request);

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual(6, packageManagerLines.Length);
            Assert.AreEqual($"source {Constants.OfficialNuGetUrlV3}", packageManagerLines[1]);
        }

        [TestMethod]
        public void When_multiple_modules_in_the_build_official_nuget_source_is_not_allowed() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            string[] packageManagerLines;

            var request = new ResolverRequest(NullLogger.Default);
            request.AddModule("C:\\Module1");
            request.AddModule("C:\\Module2");

            using (var packageManager = CreatePackageManager(fs)) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("SomeOtherPackage", "Main"),
                    },
                    request);

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual(4, packageManagerLines.Length);
            Assert.AreNotEqual($"source {Constants.OfficialNuGetUrlV3}", packageManagerLines[1]);
        }

        [TestMethod]
        public void When_using_single_file_http_sources_are_not_duplicated() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());

            var request = new ResolverRequest(NullLogger.Default);
            request.AddModule("C:\\Module1");

            using (var packageManager = CreatePackageManager(fs)) {
                packageManager.Add(
                    new[] {
                        new RemoteFile("Foo", "https://my-file-host", "main")
                    },
                    request);

                var packageManagerLines = packageManager.Lines;
                Assert.AreEqual(4, packageManagerLines.Length);
                Assert.AreEqual("http https://my-file-host Foo", packageManagerLines[packageManagerLines.Length - 2]);
            }
        }

        /// <summary>
        /// When a single package exists then a new package can replace the existing package.
        /// </summary>
        [TestMethod]
        public void When_ValidatePackageConstraints_is_true_updating_a_version_is_allowed() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());

            var request = new ResolverRequest(NullLogger.Default);
            request.AddModule("C:\\Module1");
            request.ValidatePackageConstraints = true;

            using (var packageManager = CreatePackageManager(fs)) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("SomeOtherPackage", "Main", new VersionRequirement() { ConstraintExpression = ">= 1.0.5"}),
                    },
                    request);

                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("SomeOtherPackage", "Main"),
                    },
                    request);

                Assert.AreEqual("SomeOtherPackage", packageManager.GetDependencies().Requirements.SingleOrDefault(s => string.IsNullOrEmpty(s.Value.ConstraintExpression)).Key);
            }
        }

        [TestMethod]
        public void Dispose_removes_logger() {
            new NullLogger();
            using (new PaketPackageManager(null, null, null, new NullLogger(), false)) {
                Assert.AreEqual(1, PaketPackageManager.GetLoggerReferences().Count);
            }

            Assert.AreEqual(0, PaketPackageManager.GetLoggerReferences().Count);
        }

        private string GetTestDirectoryPath() {
            return Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
        }
    }
}
