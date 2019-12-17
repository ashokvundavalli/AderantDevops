using Aderant.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Utilities {
    [TestClass]
    public class MemoizerTests {

        [TestMethod]
        public void True_returns_true() {
            Memoizer<object> memoizer = Memoizer<object>.True;
            Assert.IsTrue(memoizer.Evaluate(new object()));
        }
    }
}