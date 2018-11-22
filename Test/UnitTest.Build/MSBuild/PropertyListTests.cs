using System.Collections.Generic;
using Aderant.Build.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.MSBuild {

    [TestClass]
    public class PropertyListTests {

        [TestMethod]
        public void Is_case_insensitive() {

            var list = new PropertyList(new Dictionary<string, string>()) {
                { "F", "d" }
            };

            Assert.IsTrue(list.ContainsKey("f"));
        }
    }

}