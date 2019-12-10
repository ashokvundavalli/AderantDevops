﻿using System;
using System.Collections;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {

    [TestClass]
    public class ArtifactPackageHelperTests {

        [TestMethod]
        public void ParseMetadata_creates_flags_enum() {
            ArtifactType type = ArtifactType.None;
            ArtifactPackageHelper.ParseMetadata(new TaskItem("", new Hashtable { { "ArtifactType", "Branch|Prebuilt" } }), "ArtifactType", ref type);

            Assert.AreEqual(ArtifactType.Branch | ArtifactType.Prebuilt, type);
        }

        [TestMethod]
        public void ParseMetadata_creates_default_for_unrecognized_value() {
            ArtifactType type = ArtifactType.None;
            ArtifactPackageHelper.ParseMetadata(new TaskItem("", new Hashtable { { "ArtifactType", "BranchX" } }), "ArtifactType", ref type);

            Assert.AreEqual(ArtifactType.None, type);
        }

        [TestMethod]
        public void Default_packages_cannot_steal_items_from_custom_packages() {
            var file1 = new TaskItem("File1");
            file1.SetMetadata("Generated", bool.TrueString);
            file1.SetMetadata("ArtifactId", "mypackage.default");

            var file2 = new TaskItem("File1");
            file2.SetMetadata("Generated", bool.FalseString);
            file2.SetMetadata("ArtifactId", "mypackage.custom");

            var materializeArtifactPackages = ArtifactPackageHelper.MaterializeArtifactPackages(
                new ITaskItem[] {
                    file1, file2
                }, new []{ "" }, false);

            Assert.AreEqual(1, materializeArtifactPackages.Count);
            Assert.AreEqual("mypackage.custom", materializeArtifactPackages[0].Id);
        }

        [TestMethod]
        public void Is_test_package_when_name_starts_with_test() {
            string artifact = "IntegrationTest.bar";
            TaskItem artifactDefinition = new TaskItem(artifact);
            artifactDefinition.SetMetadata("Generated", bool.TrueString);
            artifactDefinition.SetMetadata("ArtifactId", artifact);

            var materializeArtifactPackages = ArtifactPackageHelper.MaterializeArtifactPackages(
                new ITaskItem[] {
                    artifactDefinition
                },
                new[] { "" },
                false);

            Assert.AreEqual(1, materializeArtifactPackages.Count);
            Assert.IsTrue(string.Equals(artifact, materializeArtifactPackages[0].Id, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(materializeArtifactPackages[0].PackageType.Contains(ArtifactPackageType.TestPackage));
        }

        [TestMethod]
        public void Is_test_package_when_name_ends_with_test() {
            string artifact = "Web.admin.tests";
            TaskItem artifactDefinition = new TaskItem(artifact);
            artifactDefinition.SetMetadata("Generated", bool.TrueString);
            artifactDefinition.SetMetadata("ArtifactId", artifact);

            var materializeArtifactPackages = ArtifactPackageHelper.MaterializeArtifactPackages(
                new ITaskItem[] {
                    artifactDefinition
                },
                new[] { "" },
                false);

            Assert.AreEqual(1, materializeArtifactPackages.Count);
            Assert.IsTrue(string.Equals(artifact, materializeArtifactPackages[0].Id, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(materializeArtifactPackages[0].PackageType.Contains(ArtifactPackageType.TestPackage));
        }
    }
}
