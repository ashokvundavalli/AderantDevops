using System.Collections.Generic;
using System.IO;
using System.Text;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Paket;
using VersionRequirement = Aderant.Build.VersionRequirement;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class PackagerTests {
        string json = @"{
  ""Major"":3,
  ""Minor"":0,
  ""Patch"":1,
  ""PreReleaseTag"":""git-version-play.4"",
  ""PreReleaseTagWithDash"":""-git-version-play.4"",
  ""PreReleaseLabel"":""git-version-play"",
  ""PreReleaseNumber"":4,
  ""BuildMetaData"":"""",
  ""BuildMetaDataPadded"":"""",
  ""FullBuildMetaData"":""Branch.git-version-play.Sha.c51b551cb502a74b7caaa204f10e2c2f251074ec"",
  ""MajorMinorPatch"":""3.0.1"",
  ""SemVer"":""3.0.1-git-version-play.4"",
  ""LegacySemVer"":""3.0.1-git-version-play4"",
  ""LegacySemVerPadded"":""3.0.1-git-version-play0004"",
  ""AssemblySemVer"":""3.0.1.0"",
  ""FullSemVer"":""3.0.1-git-version-play.4"",
  ""InformationalVersion"":""3.0.1-git-version-play.4+Branch.git-version-play.Sha.c51b551cb502a74b7caaa204f10e2c2f251074ec"",
  ""BranchName"":""git-version-play"",
  ""Sha"":""c51b551cb502a74b7caaa204f10e2c2f251074ec"",
  ""NuGetVersionV2"":""3.0.1-git-version-play0004"",
  ""NuGetVersion"":""3.0.1-git-version-play0004"",
  ""CommitsSinceVersionSource"":4,
  ""CommitsSinceVersionSourcePadded"":""0004"",
  ""CommitDate"":""2016-07-19""
}";

        [TestMethod]
        public void Branch_name_has_dashes_removed() {
            string packageVersion = Aderant.Build.Packaging.Packager.CreatePackageVersion(json);

            Assert.AreEqual("3.0.1-gitversionplay0004", packageVersion);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidPrereleaseLabel))]
        public void Package_starting_with_z_throws_exception() {
            PackageVersion.CreateVersion("zÆ", "lol");
        }

        [TestMethod]
        public void Unstable_label_throws_no_exceptions() {
            PackageVersion.CreateVersion("unstable", "lol");
        }

        [TestMethod]
        public void Adding_new_dependencies_to_template_preserves_document_structure() {
            var dict = new Dictionary<Domain.PackageName, Paket.VersionRequirement>();
            dict.Add(Domain.PackageName("Foo"), Paket.VersionRequirement.AllReleases);

            MemoryStream stream = null;

            var dependencies = new Packager(null).ReplicateDependenciesToTemplate(dict, () => {
                if (stream != null) {
                    return stream = new MemoryStream();
                }
                return stream = new MemoryStream(Encoding.Default.GetBytes(Resources.test_paket_template));
            });

            Assert.AreEqual(23, dependencies.Count);

            using (var reader = new StreamReader(stream)) {
                stream.Position = 0;
                var text = reader.ReadToEnd();

                Assert.IsFalse(string.IsNullOrWhiteSpace(text));
            }
        }
    }
}