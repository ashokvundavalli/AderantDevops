using System.Linq;
using Aderant.Build.Packaging.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class NuspecTokenProcessorTests {

        [TestMethod]
        public void Can_match_multiple_tokens() {
            string text = "$hello$, how $you doing?$";

            var results = NuspecTokenProcessor.GetTokens(text).ToArray();

            CollectionAssert.Contains(results, "$hello$");
            CollectionAssert.Contains(results, "$you doing?$");
        }
    }
}