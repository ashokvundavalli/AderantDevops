using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Build.DependencyAnalyzer;

namespace UnitTest.Build.ProjectSystem.StateTracking {

    [TestClass]
    public class BuildCachePackageCheckerTests {
        [TestMethod]
        public void DoesArtifactContainProjectItem_considers_end_of_artifact_path() {
            var logger = new BuildCachePackageChecker(NullLogger.Default);

            var artifacts = new ArtifactManifest {
                Id = "",
                Files = new List<ArtifactItem> {
                    new ArtifactItem {
                        File = @"Foo\Bar\MyFile.dll"
                    }
                }
            };

            var tcp = new TestConfiguredProject(null);
            tcp.OutputPath = @"..\..\Bin\Test\Foo\Bar";
            tcp.outputAssembly = "MyFile";
            tcp.OutputType = "library";
            tcp.IsWebProject = false;

            logger.Artifacts = new List<ArtifactManifest> { artifacts };
            var doesArtifactContainProjectItem = logger.DoesArtifactContainProjectItem(tcp);

            Assert.IsTrue(doesArtifactContainProjectItem);
        }
    }
}