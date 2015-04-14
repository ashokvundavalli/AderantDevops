using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
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

        [TestMethod]
        public void Mapping_of_manifest_entry_with_custom_attributes() {
            var module = new ExpertModule(XElement.Parse(@"<Module Name='UIAutomation.Framework' 
AssemblyVersion='5.3.1.0' 
GetAction='specificdroplocation' 
Path='\\na.aderant.com\packages\Infrastructure\Automation\UIAutomation' 
ExcludeFromPackaging='true' />"));

            Assert.AreEqual("5.3.1.0", module.AssemblyVersion);
            Assert.AreEqual(@"\\na.aderant.com\packages\Infrastructure\Automation\UIAutomation", module.Branch);
            Assert.AreEqual(GetAction.SpecificDropLocation, module.GetAction);
            Assert.AreEqual(1, module.CustomAttributes.Count);
            
            ExpertModuleMapper mapper = new ExpertModuleMapper();

            XElement element = mapper.Save(new[] {module}, true);

            XAttribute attribute = element.Element("Module").Attribute("ExcludeFromPackaging");

            Assert.IsNotNull(attribute);
            Assert.AreEqual(true, bool.Parse(attribute.Value));
        }

        [TestMethod]
        [DeploymentItem("Resources\\BadBuildLog.txt")]
        [DeploymentItem("Resources\\GoodBuildLog.txt")]
        public void CheckLog_returns_true_for_log_with_no_errors() {
            var result = ExpertModule.CheckLog("GoodBuildLog.txt");

            Assert.IsTrue(result);
        }

        [TestMethod]
        [DeploymentItem("Resources\\BadBuildLog.txt")]
        [DeploymentItem("Resources\\GoodBuildLog.txt")]
        public void CheckLog_returns_false_for_log_with_errors() {
            var result = ExpertModule.CheckLog("BadBuildLog.txt");

            Assert.IsFalse(result);
        }
    }
}