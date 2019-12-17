using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {

    [TestClass]
    public class GenerateBuildPlanTests {

        [TestMethod]
        public void Parse_BuildCacheOptions_no_exception() {
            var plan = new GenerateBuildPlan();
            plan.BuildCacheOptions = BuildCacheOptions.DoNotDisableCacheWhenProjectChanged.ToString();
        }
    }
}
