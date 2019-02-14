using System;
using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.PipelineService;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class DirectoryMetadataServiceTests {

        [TestMethod]
        public void Directory_metadata_roundtrip() {
            var id = DateTime.Now.Ticks.ToString();

            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(id);

                using (var impl = BuildPipelineServiceClient.GetProxy(id)) {
                    impl.AddBuildDirectoryContributor(new BuildDirectoryContribution("A"));
                    IReadOnlyCollection<BuildDirectoryContribution> metadata = impl.GetContributors();

                    Assert.AreEqual(1, metadata.Count);
                }
            }
        }
    }
}