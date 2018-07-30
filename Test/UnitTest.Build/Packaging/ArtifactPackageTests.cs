using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class ArtifactPackageTests {

        [TestMethod]
        public void Vso_storage_path_is_full_path_minus_name() {
            // Damn build systems. So you would think that TFS would take the path verbatim and just store that away.
            // But no, it takes the UNC path you give it and then when the garbage collection occurs it appends the artifact name as a folder
            // to that original path as the final path to delete. 
            // This means the web UI for a build will always point to the root folder, which is useless for usability and we need to 
            // set the actual final folder as the name.

            ArtifactStorageInfo storageInfo = new ArtifactStorageInfo {
                FullPath = @"\\some\san\storage\1\foo\bin",
                Name = @"1\foo\bin"
            };

            string vsoPath = storageInfo.ComputeVsoPath();

            Assert.AreEqual(@"\\some\san\storage", vsoPath);
        }
    }
}
