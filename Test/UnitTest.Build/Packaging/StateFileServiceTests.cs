using System;
using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.Commands;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Packaging {

    [TestClass]
    public class StateFileServiceTests {

        [TestMethod]
        public void IsFileTrustworthy_Yes() {
            var buildStateFile = new BuildStateFile {
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { "a", null }
                },
                Artifacts = new ArtifactCollection {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsTrue(artifactService.IsFileTrustworthy(buildStateFile, null, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.Candidate, artifactCacheValidationEnum);
        }

        [TestMethod]
        public void IsFileTrustworthy_Invalid_Outputs() {
            var buildStateFile = new BuildStateFile {
                ScmBranch = "refs/heads/master",
                Artifacts = new ArtifactCollection {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsFalse(artifactService.IsFileTrustworthy(buildStateFile, null, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.NoOutputs, artifactCacheValidationEnum);
        }

        [TestMethod]
        public void IsFileTrustworthy_Invalid_ArtifactCollection() {
            var buildStateFile = new BuildStateFile {
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsFalse(artifactService.IsFileTrustworthy(buildStateFile, null, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.NoArtifacts, artifactCacheValidationEnum);
        }

        [TestMethod]
        public void IsFileTrustworthy_Corrupt_ArtifactCollection() {
            var buildStateFile = new BuildStateFile {
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { @"C:\B\476\1\s\a", new ProjectOutputSnapshot() }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsFalse(artifactService.IsFileTrustworthy(buildStateFile, null, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.Corrupt, artifactCacheValidationEnum);
        }

        [TestMethod]
        [Description("'Release' flavor builds should accept 'Release' artifacts.")]
        public void IsFileTrustworthy_Release_BuildFlavor_Release_Artifact() {
            var buildStateFile = new BuildStateFile {
                BuildConfiguration = new Dictionary<string, string>(1) {
                    { "Flavor", "Release" }
                },
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { "a", null }
                },
                Artifacts = new ArtifactCollection {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsTrue(artifactService.IsFileTrustworthy(buildStateFile, new BuildStateQueryOptions { BuildFlavor = "Release" }, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.Candidate, artifactCacheValidationEnum);
        }

        [TestMethod]
        [Description("'Release' flavor builds should not accept 'Debug' artifacts.")]
        public void IsFileTrustworthy_Release_BuildFlavor_Debug_Artifact() {
            var buildStateFile = new BuildStateFile {
                ScmBranch = "refs/heads/master",
                BuildConfiguration = new Dictionary<string, string>(1) {
                    { "Flavor", "Debug" }
                },
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { "a", null }
                },
                Artifacts = new ArtifactCollection {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsFalse(artifactService.IsFileTrustworthy(buildStateFile, new BuildStateQueryOptions { BuildFlavor = "Release" }, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.BuildConfigurationMismatch, artifactCacheValidationEnum);
        }

        [TestMethod]
        [Description("'Debug' flavor builds should accept 'Release' artifacts.")]
        public void IsFileTrustworthy_Debug_BuildFlavor_Release_Artifact() {
            var buildStateFile = new BuildStateFile {
                ScmBranch = "refs/heads/master",
                BuildConfiguration = new Dictionary<string, string>(1) {
                    { "Flavor", "Release" }
                },
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { "a", null }
                },
                Artifacts = new ArtifactCollection {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsTrue(artifactService.IsFileTrustworthy(buildStateFile, new BuildStateQueryOptions { BuildFlavor = "Debug" }, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.Candidate, artifactCacheValidationEnum);
        }

        [TestMethod]
        [Description("'Debug' flavor builds should accept 'Debug' artifacts.")]
        public void IsFileTrustworthy_Debug_BuildFlavor_Debug_Artifact() {
            var buildStateFile = new BuildStateFile {
                ScmBranch = "refs/heads/master",
                BuildConfiguration = new Dictionary<string, string>(1) {
                    { "Flavor", "Debug" }
                },
                Outputs = new Dictionary<string, ProjectOutputSnapshot>(1) {
                    { "a", null }
                },
                Artifacts = new ArtifactCollection {
                    { "a", null }
                }
            };

            var artifactService = new StateFileService(new NullLogger());

            Assert.IsTrue(artifactService.IsFileTrustworthy(buildStateFile, new BuildStateQueryOptions { BuildFlavor = "Debug" }, out string reason, out ArtifactCacheValidationReason artifactCacheValidationEnum));
            Assert.IsNotNull(reason);
            Assert.AreEqual(ArtifactCacheValidationReason.Candidate, artifactCacheValidationEnum);
        }


        [TestMethod]
        public void ZeroId_is_allowed_for_tests() {
            var result = StateFileService.OrderBuildsByBuildNumber(new[] { "0", "5", "8" }, true);

            CollectionAssert.AreEquivalent(new[] { "8", "5", "0" }, result);
        }

        [TestMethod]
        public void Build_number_ordering() {
            var result = StateFileService.OrderBuildsByBuildNumber(new[] {
                @"\\aderant.com\expert-ci\prebuilts\v1\7b\4afd2ed6560319e6cb7631c07e783fff8fa865\1159766",
                @"\\aderant.com\expert-ci\prebuilts\v1\7b\4afd2ed6560319e6cb7631c07e783fff8fa865\1159767",
                @"\\aderant.com\expert-ci\prebuilts\v1\7b\4afd2ed6560319e6cb7631c07e783fff8fa865\1159775",
                @"\\aderant.com\expert-ci\prebuilts\v1\7b\4afd2ed6560319e6cb7631c07e783fff8fa865\1159777",
            }, true);

            Assert.AreEqual(@"\\aderant.com\expert-ci\prebuilts\v1\7b\4afd2ed6560319e6cb7631c07e783fff8fa865\1159777", result[0]);
        }

        [TestMethod]
        public void Comparer_sorts_id_descending() {
            var ids = new[] {
                new BuildStateFile { BuildId = 1159766 },
                new BuildStateFile { BuildId = 1159767 },
                new BuildStateFile { BuildId = 1159775 },
                new BuildStateFile { BuildId = 1159777 }
            };

            Array.Sort(ids, BuildStateFileComparer.Default);

            Assert.AreEqual(1159777, ids[0].BuildId);
        }
    }
}
