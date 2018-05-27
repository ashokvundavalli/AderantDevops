using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class DependencyManifestTests {
        [TestMethod]
        public void Can_parse_DependencyReplicationEnabled() {
            var dependencyManifest = new DependencyManifest("Module1", XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest DependencyReplication=""false"">
    <ReferencedModules>
        <ReferencedModule Name='Module0' AssemblyVersion='1.8.0.0' />    
    </ReferencedModules>
</DependencyManifest>"));

            Assert.IsFalse(dependencyManifest.DependencyReplicationEnabled.Value);
        }

        [TestMethod]
        public void DependencyReplicationEnabled_default_is_null() {
            var dependencyManifest = new DependencyManifest("Module1", XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='Module0' AssemblyVersion='1.8.0.0' />    
    </ReferencedModules>
</DependencyManifest>"));

            Assert.IsNull(dependencyManifest.DependencyReplicationEnabled);
        }

        [TestMethod]
        public void Parse_v2_DependencyManifest() {
            DependencyManifest dependencyManifest = DependencyManifestParser.Parse(Resources.DependencyManifest);

            Assert.AreEqual(1, dependencyManifest.Requirements.Count);
        }
    }

}