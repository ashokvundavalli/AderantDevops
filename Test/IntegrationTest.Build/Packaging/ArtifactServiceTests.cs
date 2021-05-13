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

        [TestMethod]
        public void UpdateFile() {
            var tempFileName = Path.GetTempFileName();

            ArtifactService artifactService = new ArtifactService(null, new PhysicalFileSystem(), new NullLogger());

            var writes = new[] {
                new PathSpec(tempFileName, tempFileName)
            };
            artifactService.UpdateWriteTimes(writes);

            foreach (PathSpec pathSpec in writes) {
                // Select the time for the file.
                Assert.AreEqual(artifactService.WriteTime.Year, File.GetCreationTimeUtc(pathSpec.Destination).Year);
                Assert.AreEqual(artifactService.WriteTime.Year, File.GetLastWriteTimeUtc(pathSpec.Destination).Year);
            }
        }
    }
}
