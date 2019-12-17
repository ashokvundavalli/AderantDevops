using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class ArtifactStagingPathBuilderTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CreateStagingPathWithBucketId() {
            SourceTreeMetadata metadata = new SourceTreeMetadata {
                BucketIds = new HashSet<BucketId> { new BucketId("1d98d9296eae891d3d4adb7c884bc2df12504619", "a") }
            };

            ArtifactStagingPathBuilder artifactStagingPathBuilder = new ArtifactStagingPathBuilder(TestContext.DeploymentDirectory, 0, metadata);

            bool sendToArtifactCache;
            string path = artifactStagingPathBuilder.CreatePath("a", out sendToArtifactCache);

            Assert.AreEqual(true, sendToArtifactCache);
            Assert.AreEqual(Path.Combine(TestContext.DeploymentDirectory, "_artifacts", BucketId.CreateDirectorySegment(metadata.BucketIds.First().Id), "0"), path);
        }

        [TestMethod]
        public void CreateStagingPathWithNoBucketId() {
            SourceTreeMetadata metadata = new SourceTreeMetadata {
                BucketIds = new HashSet<BucketId>()
            };

            ArtifactStagingPathBuilder artifactStagingPathBuilder = new ArtifactStagingPathBuilder(TestContext.DeploymentDirectory, 0, metadata);

            bool sendToArtifactCache;
            string path = artifactStagingPathBuilder.CreatePath("a", out sendToArtifactCache);

            Assert.AreEqual(false, sendToArtifactCache);
            Assert.AreEqual(Path.Combine(TestContext.DeploymentDirectory, "_artifacts", "a", "0"), path);
        }
    }
}
