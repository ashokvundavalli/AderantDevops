﻿using System.IO;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Models;
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
            using (var packageManager = new PaketPackageManager(GetTestDirectoryPath(), fs.Object, new NullLogger())) {
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

        [TestMethod]
        public void Local_package_server_is_listed_first() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            string[] packageManagerLines;
            using (var packageManager = new PaketPackageManager(GetTestDirectoryPath(), fs.Object, new NullLogger())) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("Foo", "Main"),
                        DependencyRequirement.Create("Foo", "Bar"),
                    });

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual(packageManagerLines[0], "source " + Constants.PackageServerUrl);
        }

        [TestMethod]
        [ExpectedException(typeof(DependencyException))]
        public void Package_manager_fails_on_constraint_conflicts() {
            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            using (var packageManager = new PaketPackageManager(GetTestDirectoryPath(), fs.Object, new NullLogger())) {
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

            using (var packageManager = new PaketPackageManager(
                GetTestDirectoryPath(),
                new Mock<IFileSystem2>().Object,
                NullLogger.Default)) {
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

            using (var packageManager = new PaketPackageManager(GetTestDirectoryPath(), fs.Object, new NullLogger())) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("Aderant.Database.Backup", "Main"),
                    },
                    request);

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual($"source {Constants.PackageServerUrl}", packageManagerLines[0]);
            Assert.AreEqual($"source {Constants.DatabasePackageUri}", packageManagerLines[1]);
        }

        [TestMethod]
        public void When_a_single_module_is_in_the_build_official_nuget_source_allowed() {
            string lines = @"
source https://www.nuget.org/api/v2
nuget ThePackageFromNuget";

            var fs = new Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(GetTestDirectoryPath());
            string[] packageManagerLines;

            var request = new ResolverRequest(NullLogger.Default);
            request.AddModule("C:\\SingleModule");

            using (var packageManager = new PaketPackageManager(GetTestDirectoryPath(), fs.Object, new NullLogger())) {
                packageManager.SetDependenciesFile(lines);

                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("SomeOtherPackage", "Main"),
                    },
                    request);

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual(4, packageManagerLines.Length);
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

            using (var packageManager = new PaketPackageManager(GetTestDirectoryPath(), fs.Object, new NullLogger())) {
                packageManager.Add(
                    new[] {
                        DependencyRequirement.Create("SomeOtherPackage", "Main"),
                    },
                    request);

                packageManagerLines = packageManager.Lines;
            }

            Assert.AreEqual(2, packageManagerLines.Length);
            Assert.AreNotEqual($"source {Constants.OfficialNuGetUrlV3}", packageManagerLines[1]);
        }

        private string GetTestDirectoryPath() {
            return Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());
        }
    }

}