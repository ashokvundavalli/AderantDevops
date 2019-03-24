using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    [DeploymentItem(@"Resources\LightUpManifest.xml")]
    public class LightUpManifestTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [Ignore]
        public void ReplaceXml() {
            string lightUpManifest = Path.Combine(TestContext.DeploymentDirectory, "LightUpManifest.xml");

            string[] referencedModule = new string[] {
                "Aderant.Disbursements",
                "GetAction:NuGet",
                "Version:>= 0 build"
            };

            List<XAttribute> attributes = new List<XAttribute> { new XAttribute("Name", referencedModule[0]) };

            foreach (string property in referencedModule.Skip(1)) {
                if (property.StartsWith("getaction:", StringComparison.OrdinalIgnoreCase) && property.ToLower() != "getaction:") {
                    attributes.Add(new XAttribute("GetAction", property.Split(new char[] { ':' }, 2)[1]));
                    continue;
                }

                if (property.StartsWith("assemblyversion:", StringComparison.OrdinalIgnoreCase) && property.ToLower() != "assemblyversion:") {
                    attributes.Add(new XAttribute("AssemblyVersion", property.Split(new char[] { ':' }, 2)[1]));
                    continue;
                }

                if (property.StartsWith("version:", StringComparison.OrdinalIgnoreCase) && property.ToLower() != "version:") {
                    attributes.Add(new XAttribute("Version", property.Split(new char[] { ':' }, 2)[1].Trim()));
                }
            }

            XElement manifest = XElement.Load(lightUpManifest);
            manifest.XPathSelectElement("/ReferencedModules").Add(new XElement("ReferencedModule", attributes.ToArray()));
            manifest.Save(lightUpManifest);

            string[] alteredXml = File.ReadAllLines(lightUpManifest);

            foreach (string line in alteredXml) {
                if (line.Contains("<ReferencedModule Name=\"Aderant.Disbursements\" GetAction=\"NuGet\" Version=\"&gt;= 0 build\" />")) {
                    return;
                }
            }

            Assert.Fail("LightUpManifest.xml was not updated correctly.");
        }
    }
}