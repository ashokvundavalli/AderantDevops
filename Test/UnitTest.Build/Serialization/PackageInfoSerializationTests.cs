using System.Collections.Generic;
using Aderant.Build.DependencyResolver.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Serialization {
    [TestClass]
    public class PackageInfoTests : SerializationBase {

        [TestMethod]
        public void PackageInfoSerializeTest() {
            PackageInfo packageInfo = new PackageInfo("Test", "1.0.0");

            var result = RoundTrip(packageInfo);

            Assert.IsNotNull(result);
            Assert.AreEqual(packageInfo.Name, result.Name);
            Assert.AreEqual(packageInfo.Version, result.Version);
        }

        [TestMethod]
        public void PackageInfoGroupSerializeTest() {
            PackageInfo packageInfo = new PackageInfo("Test", "1.0.0");

            PackageGroup packageGroup = new PackageGroup("Test", new List<PackageInfo>(1) {
                packageInfo
            });

            var result = RoundTrip(packageGroup);

            Assert.IsNotNull(result);
            Assert.AreEqual(packageGroup.Name, result.Name);
            Assert.AreEqual(packageGroup.PackageInfo.Count, result.PackageInfo.Count);
        }
    }
}
