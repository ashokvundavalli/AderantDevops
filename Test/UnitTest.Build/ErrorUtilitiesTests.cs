using System;
using Aderant.Build;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ErrorUtilitiesTests {

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Throw() {
            ErrorUtilities.IsNotNull((object)null, "bang");
        }
    }
}
