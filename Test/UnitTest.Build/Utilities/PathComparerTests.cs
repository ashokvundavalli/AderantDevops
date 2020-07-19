using System.Collections.Generic;
using System.Linq;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Utilities {
    [TestClass]
    public class PathComparerTests {

        [TestMethod]
        public void Trailing_slash_is_ignored() {
            IEnumerable<string> results = new[] { "path1\\", "path1" }.Distinct(PathUtility.PathComparer);
            Assert.AreEqual(1, results.Count());
        }
    }
}