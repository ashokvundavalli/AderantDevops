using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
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

            XElement element = mapper.Save(new[] { module }, true);

            XAttribute attribute = element.Element("Module").Attribute("ExcludeFromPackaging");

            Assert.IsNotNull(attribute);
            Assert.AreEqual(true, bool.Parse(attribute.Value));
        }

        [TestMethod]
        [DeploymentItem("Resources\\BadBuildLog.txt")]
        [DeploymentItem("Resources\\GoodBuildLog.txt")]
        public void CheckLog_returns_true_for_log_with_no_errors() {
            var result = FolderDependencySystem.CheckLog("GoodBuildLog.txt");

            Assert.IsTrue(result);
        }

        [TestMethod]
        [DeploymentItem("Resources\\BadBuildLog.txt")]
        [DeploymentItem("Resources\\GoodBuildLog.txt")]
        public void CheckLog_returns_false_for_log_with_errors() {
            var result = FolderDependencySystem.CheckLog("BadBuildLog.txt");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void OrderBuildsByBuildNumber_orders_paths_descending() {
            List<string> builds = new List<string>();

            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5430.62979");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5431.62889");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5434.37111");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5434.62889");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5435.62894");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5436.24601");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5451.5547");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5452.5581");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5456.34701");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5456.40407");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5458.25024");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5463.24158");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5466.5608");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5491.43530");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5494.49271");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5497.49272");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5498.49270");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5499.49230");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5505.49237");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5514.49237");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5518.49253");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5519.49230");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5521.49245");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5525.49270");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5526.49267");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5527.49235");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5528.31467");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5528.49230");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5529.49234");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5532.49259");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5533.49277");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5534.49265");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5535.49229");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5536.49258");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5539.49240");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5540.49236");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5541.49234");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5542.49231");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5543.49274");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5546.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5547.49247");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5548.49240");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5549.31843 (Libraries.SoftwareFactory)");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5549.49276");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5550.49243");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5553.49258");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5554.31047");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5554.49264");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5555.31937");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5555.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5556.49235");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5557.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5560.49269");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5561.49271");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5562.49238");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5563.49245");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5564.49247");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5567.49274");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5568.49242");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5569.49240");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5575.49267");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5576.49229");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5576.5565");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5577.24722");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5577.49241");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5578.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5581.49286");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5581.5591");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5582.49253");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5582.5567");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5583.49241");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5583.5618");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5584.49239");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5584.5604");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.40258");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.43042");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.44331");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.47010");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.47180");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.5607");

            var actual = FolderDependencySystem.OrderBuildsByBuildNumber(builds.ToArray()).First();

            Assert.AreEqual(@"\\na.aderant.com\expertsuite\Dev\MP2\Libraries.SoftwareFactory\1.8.0.0\1.8.5585.47180", actual, true);

        }

        [TestMethod]
        public void OrderBuildsByBuildNumber_orders_revisions_descending() {
            List<string> builds = new List<string>();

            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5430.62979");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5431.62889");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5434.37111");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5434.62889");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5435.62894");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5436.24601");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5451.5547");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5452.5581");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5456.34701");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5456.40407");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5458.25024");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5463.24158");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5466.5608");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5491.43530");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5494.49271");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5497.49272");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5498.49270");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5499.49230");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5505.49237");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5514.49237");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5518.49253");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5519.49230");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5521.49245");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5525.49270");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5526.49267");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5527.49235");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5528.31467");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5528.49230");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5529.49234");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5532.49259");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5533.49277");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5534.49265");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5535.49229");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5536.49258");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5539.49240");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5540.49236");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5541.49234");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5542.49231");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5543.49274");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5546.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5547.49247");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5548.49240");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5549.31843 (Libraries.SoftwareFactory)");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5549.49276");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5550.49243");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5553.49258");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5554.31047");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5554.49264");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5555.31937");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5555.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5556.49235");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5557.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5560.49269");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5561.49271");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5562.49238");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5563.49245");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5564.49247");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5567.49274");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5568.49242");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5569.49240");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5575.49267");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5576.49229");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5576.5565");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5577.24722");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5577.49241");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5578.49278");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5581.49286");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5581.5591");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5582.49253");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5582.5567");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5583.49241");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5583.5618");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5584.49239");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5584.5604");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.40258");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.43042");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.44331");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.47010");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.47180");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5585.5607");
            builds.Add("\\\\na.aderant.com\\expertsuite\\dev\\mp2\\Libraries.SoftwareFactory\\1.8.0.0\\1.8.5586.5500");

            var actual = FolderDependencySystem.OrderBuildsByBuildNumber(builds.ToArray()).First();

            Assert.AreEqual(@"\\na.aderant.com\expertsuite\Dev\MP2\Libraries.SoftwareFactory\1.8.0.0\1.8.5586.5500", actual, true);

        }

        [TestMethod]
        public void Mapping_of_manifest_entry_with_NuGet_source() {
            var module = new ExpertModule(XElement.Parse(@"<Module Name='MyModule' AssemblyVersion='5.3.1.0' GetAction='NuGet' />"));

            Assert.AreEqual(GetAction.NuGet, module.GetAction);
        }

        [TestMethod]
        public void When_branch_is_set_repository_cannot_be_nuget() {
            var module = ExpertModule.Create(XElement.Parse(@"<Module Name='Marketing.Help' Branch='Main' />"));

            Assert.AreEqual(module.RepositoryType, RepositoryType.Folder);
        }

        [TestMethod]
        public void ModuleType_parsing() {
            Assert.AreEqual(ModuleType.Library, ExpertModule.GetModuleType("Libraries.Foo"));
            Assert.AreEqual(ModuleType.Service, ExpertModule.GetModuleType("Services.Foo"));
            Assert.AreEqual(ModuleType.Application, ExpertModule.GetModuleType("Applications.Foo"));
            Assert.AreEqual(ModuleType.SDK, ExpertModule.GetModuleType("SDK.Foo"));
            Assert.AreEqual(ModuleType.Sample, ExpertModule.GetModuleType("Workflow.Foo"));
            Assert.AreEqual(ModuleType.Library, ExpertModule.GetModuleType("Libraries.Foo"));
            Assert.AreEqual(ModuleType.InternalTool, ExpertModule.GetModuleType("Internal.Foo"));
            Assert.AreEqual(ModuleType.Build, ExpertModule.GetModuleType("BUILD.Foo"));
            Assert.AreEqual(ModuleType.Web, ExpertModule.GetModuleType("Web.Foo"));
            Assert.AreEqual(ModuleType.Web, ExpertModule.GetModuleType("Mobile.Foo"));
            Assert.AreEqual(ModuleType.Installs, ExpertModule.GetModuleType("Installs.Foo"));
            Assert.AreEqual(ModuleType.Test, ExpertModule.GetModuleType("Tests.Foo"));
            Assert.AreEqual(ModuleType.Performance, ExpertModule.GetModuleType("Performance.Foo"));
            Assert.AreEqual(ModuleType.Database, ExpertModule.GetModuleType("Database.Foo"));
        }

        [TestMethod]
        public void ThirdParty_module_name_parsing() {
            Assert.AreEqual(ModuleType.ThirdParty, ExpertModule.GetModuleType("THIRDPARTY.Foo"));
            Assert.AreEqual(ModuleType.Help, ExpertModule.GetModuleType("Marketing.Help"));
        }

        [TestMethod]
        public void IsOneOf_can_returns_true_when_name_matches() {
            Assert.IsTrue("Marketing.Help".IsOneOf(ModuleType.Help));
        }
    }
}