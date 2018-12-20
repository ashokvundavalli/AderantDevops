using System;
using System.Collections.Generic;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class BuildPipelineServiceImplTests {


        [TestMethod]
        public void Put_and_claim_tracked_files_over_wcf() {
            const string key = "some_key";

            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(DateTime.Now.Ticks.ToString());

                using (var impl = BuildPipelineServiceClient.Current) {

                    impl.TrackInputFileDependencies(key, new List<TrackedInputFile> { new TrackedInputFile("") });

                    IReadOnlyCollection<TrackedInputFile> files1 = impl.ClaimTrackedInputFiles(key);

                    Assert.IsNotNull(files1);
                    Assert.AreEqual(1, files1.Count);

                    var files2 = impl.ClaimTrackedInputFiles(key);

                    Assert.IsNull(files2);
                }
            }
        }
    }
}