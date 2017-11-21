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
            var manifest = new ExpertManifest(XDocument.Parse(manifestText));
            var element = manifest.MergeAttributes(entry);

            Assert.AreEqual(4, element.Attributes().Count());
            Assert.AreEqual("Main", element.Attribute("Path").Value);
            Assert.AreEqual("1.9.0.0", element.Attribute("AssemblyVersion").Value);
            Assert.AreEqual("branch", element.Attribute("GetAction").Value);
        }

        [TestMethod]
        public void Version_attribute_creates_version_constraint() {
            string manifestText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ProductManifest Name=""Expert"" ExpertVersion=""0"">
  <Modules>
    <Module Name=""Foo"" Version=""&lt;= 8.1.1"" GetAction=""NuGet"" />
</Modules>
</ProductManifest>";

            var manifest = new ExpertManifest(XDocument.Parse(manifestText));

            var foo = manifest.GetModule("Foo");
            Assert.IsNotNull(foo.VersionRequirement);
            Assert.AreEqual("<= 8.1.1", foo.VersionRequirement.ConstraintExpression);
        }

        [TestMethod]
        public void ReplicateToDependencies_attribute() {
            string manifestText = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ProductManifest Name=""Expert"" ExpertVersion=""0"">
  <Modules>
    <Module Name=""Foo"" Version=""&lt;= 8.1.1"" GetAction=""NuGet"" ReplicateToDependencies=""false"" />
</Modules>
</ProductManifest>";

            var manifest = new ExpertManifest(XDocument.Parse(manifestText));

            var foo = manifest.GetModule("Foo");
            Assert.AreEqual(false, foo.ReplicateToDependencies);
        }
    }
}