using System.Collections.Generic;
using Aderant.Build.DependencyResolver.Model;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Serialization {
    [TestClass]
    public class TrackedMetadataFileTest : SerializationBase {
        [TestMethod]
        public void PackageInfoSerializeTest() {
            TrackedMetadataFile trackedMetadataFile = new TrackedMetadataFile("Test") {
                PackageHash = "asdf",
                PackageGroups = new List<PackageGroup>(1) {
                    new PackageGroup("Test", new List<PackageInfo>(1) {
                        new PackageInfo("Test", "1")
                    })
                }
            };

            var result = RoundTrip(trackedMetadataFile);
            Assert.IsNotNull(result);
            Assert.AreEqual(trackedMetadataFile.PackageHash, result.PackageHash);
            Assert.AreEqual(trackedMetadataFile.PackageGroups.Count, result.PackageGroups.Count);
        }
    }
}
