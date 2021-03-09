using Aderant.Build.Packaging;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build;

namespace IntegrationTest.Build.Packaging {
    [TestClass]
    public class ArtifactServiceTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Resources", "Resources")]
        public void UpdateFile() {
            string fileDirectory = Path.Combine(TestContext.DeploymentDirectory, "Resources", "BinFiles", "Customization"); // Nested Folder

            ArtifactService artifactService = new ArtifactService(null, new PhysicalFileSystem(), new NullLogger());

            IList<PathSpec> files = Directory.GetFiles(fileDirectory, "*", SearchOption.AllDirectories).Select(x => new PathSpec(x, x)).ToList();

            artifactService.UpdateWriteTimes(files);

            foreach (PathSpec pathSpec in files) {
                // Select the time for the file.
                Assert.AreEqual(artifactService.WriteTime.Year, File.GetLastAccessTimeUtc(pathSpec.Destination).Year);
                Assert.AreEqual(artifactService.WriteTime.Year, File.GetCreationTimeUtc(pathSpec.Destination).Year);
                Assert.AreEqual(artifactService.WriteTime.Year, File.GetLastWriteTimeUtc(pathSpec.Destination).Year);
            }
        }
    }
}
