using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ExpertManifestTests {

        [TestMethod]
        public void Attribute_merge_preserves_dependency_manifest_attributes() {
            var entry = XElement.Parse(@"<ReferencedModule Name=""Applications.Foo"" AssemblyVersion=""1.9.0.0"" GetAction=""branch"" Path=""Main"" />");

            string manifestText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ProductManifest Name=""Expert"" ExpertVersion=""802"">
  <Modules>
    <Module Name=""Applications.Foo"" AssemblyVersion=""1.8.0.0""  />
</Modules>
</ProductManifest>
";
            var manifest = new ExpertManifest(null, XDocument.Parse(manifestText));
            var element = manifest.MergeAttributes(entry);

            Assert.AreEqual(4, element.Attributes().Count());
            Assert.AreEqual("Main", element.Attribute("Path").Value);
            Assert.AreEqual("1.9.0.0", element.Attribute("AssemblyVersion").Value);
            Assert.AreEqual("branch", element.Attribute("GetAction").Value);
        }
    }
}