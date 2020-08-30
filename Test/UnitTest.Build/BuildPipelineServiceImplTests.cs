using System;
using System.Collections.Generic;
using System.Threading;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class BuildPipelineServiceImplTests {
        [TestMethod]
        public void Put_and_claim_tracked_files_over_wcf() {
            const string key = "some_key";

            var id = DateTime.Now.Ticks.ToString();

            using (var host = new BuildPipelineServiceHost()) {
                host.StartService(id);

                using (var impl = BuildPipelineServiceClient.GetProxy(id)) {
                    impl.TrackInputFileDependencies(key, new List<TrackedInputFile> {
                        new TrackedInputFile("abc")
                    });

                    IReadOnlyCollection<TrackedInputFile> files1 = null;

                    // Write side of the contract is 1-way so spin until the write is done
                    if (!SpinWait.SpinUntil(() => (files1 = impl.ClaimTrackedInputFiles(key)) != null, TimeSpan.FromMilliseconds(5000))) {
                        Assert.Fail();
                    }

                    Assert.IsNotNull(files1);
                    Assert.AreEqual(1, files1.Count);

                    var files2 = impl.ClaimTrackedInputFiles(key);

                    Assert.IsNull(files2);
                }
            }
        }
    }
}