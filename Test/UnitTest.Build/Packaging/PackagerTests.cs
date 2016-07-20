using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}
