using System.Collections.Generic;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System;
using System.Xml.Linq;

namespace UnitTest.Build.Tasks {
    [TestClass]
    [DeploymentItem(@"Tasks\Roles", @"Tasks\Roles")]
    public class RoleManifestPackageIdentifierTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DetectDuplicateRoleFiles() {
            var roleManifests = new [] {
                new RoleManifest("a", new XDocument()),
                new RoleManifest("a", new XDocument())
            };

            Assert.IsTrue(RoleManifestPackageIdentifier.DuplicateRoleFilesPresent(roleManifests, null));
        }

        [TestMethod]
        public void DetectDuplicateRoleFilesNoDuplicates() {
            var roleManifests = new [] {
                new RoleManifest("a", new XDocument()),
                new RoleManifest("b", new XDocument())
            };

            Assert.IsFalse(RoleManifestPackageIdentifier.DuplicateRoleFilesPresent(roleManifests, null));
        }

        [TestMethod]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        public void DetectNonExistingRoleManifestDirectory() {
            const string nonExistingPath = "Mikrokosmos";

            string[] manifestSearchDirectories = new[] {
                nonExistingPath
            };

            RoleManifestPackageIdentifier.LocateRoleManifests(manifestSearchDirectories);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DetectNullRoleManifestDirectory() {
            RoleManifestPackageIdentifier.LocateRoleManifests(null);
        }

        [TestMethod]
        public void DetectExistingRoleManifestDirectory() {
            string existingPath = Path.Combine(TestContext.DeploymentDirectory, @"Tasks\Roles");

            string[] manifestSearchDirectories = new string[] {
                existingPath
            };

            var roleManifests = RoleManifestPackageIdentifier.LocateRoleManifests(manifestSearchDirectories);
            Assert.IsNotNull(roleManifests);
        }
    }
}
