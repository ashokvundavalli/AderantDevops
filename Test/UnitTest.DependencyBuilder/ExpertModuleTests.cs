using System.Xml.Linq;
using DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.DependencyAnalyzer {
    [TestClass]
    public class ExpertModuleTests {
        
        [TestMethod]
        public void WhenSettingNamePropertyAndValueIsFullPathNameIsFolder() {
            ExpertModule m = new ExpertModule();
            m.Name = @"C:\tfs\ExpertSuite\Dev\a\Modules\Applications.Foo";

            Assert.AreEqual("Applications.Foo", m.Name);
        }

        [TestMethod]
        public void BranchIsSetUsingXElementConstructor() {
            var element = XElement.Parse(@"<Module Name='Applications.CCLogViewer' AssemblyVersion='1.8.0.0' GetAction='branch' Path='Main' />");
            
            ExpertModule m = new ExpertModule(element);
            
            Assert.AreEqual("Main", m.Branch);
        }

        [TestMethod]
        public void AssemblyVersionIsSetUsingXElementConstructor() {
            var element = XElement.Parse(@"<Module Name='Applications.CCLogViewer' AssemblyVersion='1.8.0.0' GetAction='branch' Path='Main' />");

            ExpertModule m = new ExpertModule(element);

            Assert.AreEqual("1.8.0.0", m.AssemblyVersion);
        }
    }
}