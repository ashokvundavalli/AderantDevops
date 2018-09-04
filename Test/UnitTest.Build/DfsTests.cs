using Aderant.Build;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class DfsTests {

        [TestMethod]
        public void GetShareFromPath_results_share() {
            var result = Dfs.GetShareFromPath(@"\\dfs.namesapce.com\someshare\somefolder");
            Assert.AreEqual(@"\\dfs.namesapce.com\someshare", result);
        }
    }
}
