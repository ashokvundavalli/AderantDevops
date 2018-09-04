using System.Collections;
using Aderant.Build.Packaging;
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
        public void ParseMetadata_creates_default_for_unrecongized_value() {
            ArtifactType type = ArtifactType.None;
            ArtifactPackageHelper.ParseMetadata(new TaskItem("", new Hashtable { { "ArtifactType", "BranchX" } }), "ArtifactType", ref type);

            Assert.AreEqual(ArtifactType.None, type);
        }
    }

}
