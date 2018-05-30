using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class MSBuildArgumentBuilderTests {

        [TestMethod]
        public void BuildArg() {

            var ctx = new Context();
            IArgumentBuilder argumentBuilder = ctx.CreateArgumentBuilder("MSBuild");

            var args = argumentBuilder.GetArguments(null);

            Assert.AreEqual(1, args.Length);
        }
    }
}