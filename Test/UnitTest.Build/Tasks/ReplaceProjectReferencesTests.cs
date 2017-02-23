using System.Xml.Linq;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class ReplaceProjectReferencesTests {
        [TestMethod]
        public void ProjectReferencesAreSuccessfullyReplacedWithFileReferences() {
            string expected = @"
    <Reference Include=""Aderant.Foo"">
      <Private>False</Private>
    </Reference>";

            var doc = XElement.Parse(Resources.ReplaceReferencesProject);
            ReplaceProjectReferences.ReplaceProjectReferencesInXml(doc);
            string docAsString = doc.ToString();
            Assert.IsFalse(docAsString.Contains("ProjectReference"));
            Assert.IsTrue(docAsString.Contains(expected));
        }
    }
}
