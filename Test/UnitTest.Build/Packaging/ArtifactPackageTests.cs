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

            BuildArtifact storageInfo = new BuildArtifact {
                SourcePath = @"\\some\san\storage\1\foo\bin",
                StoragePath = @"\\some\san\storage\1\foo\bin",
                Name = @"1\foo\bin"
            };

            string vsoPath = storageInfo.ComputeVsoPath();

            Assert.AreEqual(@"\\some\san\storage", vsoPath);
        }

        [TestMethod]
        public void Vso_path_is_smallest_substring() {
            BuildArtifact a = new BuildArtifact {
                SourcePath = "\\\\mydrop\\bar\\9.9.9.9\\1.0.0.0\\Bin\\Module",
                StoragePath = "\\\\mydrop\\bar\\9.9.9.9\\1.0.0.0\\Bin\\Module",
                Name = "bar\\9.9.9.9\\1.0.0.0"
            };

            var computeVsoPath = a.ComputeVsoPath();

            Assert.AreEqual("\\\\mydrop", computeVsoPath);
        }

        [TestMethod]
        public void Destination_is_filename_by_default() {
            PathSpec spec = ArtifactPackageDefinition.CreatePathSpecification(null, @"Foo\Bar\Baz.dll", null);
            Assert.AreEqual("Baz.dll", spec.Destination);
        }

        [TestMethod]
        public void Destination_is_respected_when_directory() {
            PathSpec spec = ArtifactPackageDefinition.CreatePathSpecification(null, @"Foo\Bar\Baz.dll", "Foo");
            Assert.AreEqual(@"Foo\Baz.dll", spec.Destination);
        }
    }
}
