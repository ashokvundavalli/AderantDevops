using Aderant.Build.Packaging.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class NuspecTests {

        [TestMethod]
        public void Can_amend_version_to_replacement_token() {

            var spec = new Nuspec();
            spec.Version.Value = "8.1.0-$version$";

            Assert.IsTrue(spec.Version.HasReplacementTokens);

            spec.Version.ReplaceToken("$version$", "Foo");

            Assert.AreEqual("8.1.0-Foo", spec.Version.Value);
        }

        [TestMethod]
        public void SerializeNuspec() {
            Nuspec nuspec = new Nuspec();

            string result = NuspecSerializer.Serialize(nuspec, Resources.TemplateNuspec);

            Assert.AreEqual(XDocument.Parse(Resources.TemplateNuspec).ToString(), result);
        }
    }
}
