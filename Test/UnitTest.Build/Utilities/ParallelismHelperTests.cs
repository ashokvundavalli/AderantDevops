using Aderant.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Utilities {
    [TestClass]
    public class ParallelismHelperTests {
        [TestMethod]
        public void CheckNonZeroValueIsReturned() {
            Assert.AreNotEqual(0, ParallelismHelper.MaxDegreeOfParallelism);
        }
    }
}
