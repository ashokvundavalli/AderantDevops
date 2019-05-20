using System.Collections.Generic;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System;

namespace UnitTest.Build.Tasks {
    [TestClass]
    [DeploymentItem(@"Tasks\Roles", @"Tasks\Roles")]
    public class RoleManifestPackageIdentifierTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DetectDuplicateRoleFiles() {
            List<RoleManifest> roleManifests = new List<RoleManifest>(2) {
                new RoleManifest("a", string.Empty),
                new RoleManifest("a", string.Empty)
            };

            Assert.IsTrue(RoleManifestPackageIdentifier.DuplicateRoleFilesPresent(roleManifests, null));
        }

        [TestMethod]
        public void DetectDuplicateRoleFilesNoDuplicates() {
            List<RoleManifest> roleManifests = new List<RoleManifest>(2) {
                new RoleManifest("a", string.Empty),
                new RoleManifest("b", string.Empty)
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

            RoleManifest[] roleManifests = RoleManifestPackageIdentifier.LocateRoleManifests(manifestSearchDirectories);
            Assert.IsNotNull(roleManifests);
        }
    }
}
