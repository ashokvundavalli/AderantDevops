using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class IntegrationTestBuilderTests {

        [TestMethod]
        public void ConfigureIntegrationTestAssemblyTest() {
            var mock = TestHelper.CreateMockForFileSystem();
            mock.Setup(m => m.File.Exists(It.IsAny<string>())).Returns(true);
            mock.Setup(m => m.Directory.GetFileSystemEntries(It.IsAny<string>(), "instance.config", SearchOption.AllDirectories)).Returns(new string[] { "Foo" });
            mock.Setup(m => m.File.ReadAllText(It.IsAny<string>())).Returns<string>((str) => {
                if (str.EndsWith("mydll.dll.config")) {
                    return Resources.AppConfig;
                }

                if (str.EndsWith("instance.config")) {
                    return Resources.instance;
                }

                if (str.EndsWith("clients.config")) {
                    return Resources.clients;
                }

                if (str.EndsWith("bindings.config")) {
                    return Resources.bindings;
                }

                throw new InvalidOperationException("Unexpected file");
            });
            
            var assembly = new IntegrationTestAssemblyConfig("mydll.dll", mock.Object);
            
            assembly.ConfigureAppConfig(new IntegrationTestContext(XDocument.Parse(Resources.environment)));

            var appConfig = assembly.AppConfig;

            XNamespace ns = "urn:schemas-microsoft-com:asm.v1";

            Assert.AreNotEqual(0, appConfig.Element("configuration").Element("system.serviceModel").Element("client").Descendants().Count());
            Assert.AreEqual(1, appConfig.Element("configuration").Element("runtime").Descendants(ns + "assemblyBinding").Count());
        }

        [TestMethod]
        public void TidyUpAppConfig() {
            string[] entries = Directory.GetFileSystemEntries(@"C:\tfs\expertsuite\releases\803x\modules", "app.config", SearchOption.AllDirectories);

            var header = new XComment(@"
This is the integration tests app config file that has the config burnt in for integration tests to run.

To run the integration tests locally you need to edit the following attributes
 - aderant/instanceMetadataConfigurationSection@environment             Environment name
 - aderant/instanceMetadataConfigurationSection/repository@name         Expert DB name
 - aderant/instanceMetadataConfigurationSection/repository@server       Expert DB server and instance
 - aderant/instanceMetadataConfigurationSection/expertShare@uri         Path to expert share
 - aderant/instanceMetadataConfigurationSection/queryService@uri        Query service uri
 - system.serviceModel/client/endpoint@address                          All service addresses need to be updated if you use non
                                                                        default port, and if environment <> 'Local'
");

            foreach (var entry in entries) {
                if (entry.IndexOf("test\\config", StringComparison.OrdinalIgnoreCase) > 0) {
                    var document = XDocument.Load(entry, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);

                    //foreach (XNode node in document.Nodes()) {
                    //    var comment = node as XComment;
                    //    if (comment != null) {

                    //        if (comment.Value.IndexOf("NB") > 0) {
                    //            comment.ReplaceWith(header);
                    //            break;
                    //        }
                    //    }
                    //}

    //                <system.serviceModel>
    //<extensions configSource="extensions.config" />
    //<bindings configSource="bindings.config" />
    //<client configSource="clients.config" />
    //<behaviors>
    //  <endpointBehaviors>
  //      <behavior name="LargeQuotaBehavior">
  //        <dataContractSerializer maxItemsInObjectGraph="2147483600" />
  //      </behavior>
  //    </endpointBehaviors>
  //  </behaviors>
  //</system.serviceModel>

                    var lqb = new XElement("behavior", new XAttribute("name", "LargeQuotaBehavior"),
                        new XElement("dataContractSerializer", new XAttribute("maxItemsInObjectGraph", "2147483600")));

                    bool hasLargeQuotaBehavior = false;

                    XElement configuration = document.Element("configuration");
                    if (configuration != null) {
                        var serviceModel = configuration.Element("system.serviceModel");
                        if (serviceModel != null) {
                            XElement behaviors = serviceModel.Element("behaviors");
                            if (behaviors != null) {
                                XElement endpointBehaviors = behaviors.Element("endpointBehaviors");

                                if (endpointBehaviors != null) {
                                    IEnumerable<XElement> elements = endpointBehaviors.Descendants();

                                    foreach (XElement element in elements) {
                                        XAttribute attribute = element.Attribute("name");

                                        if (attribute != null) {
                                            if (attribute.Value == "LargeQuotaBehavior") {
                                                hasLargeQuotaBehavior = true;
                                                break;
                                            }
                                        }

                                        if (!hasLargeQuotaBehavior) {
                                            endpointBehaviors.Add(lqb);
                                            hasLargeQuotaBehavior = true;
                                        }
                                    }
                                }
                            } else {
                                serviceModel.Add(new XElement("behaviors", new XElement("endpointBehaviors", lqb)));
                            }
                        }
                    }

                    document.Save(entry);
                }
            }
        }
    }

    internal static class TestHelper {
        public static Mock<FileSystem> CreateMockForFileSystem() {
            Mock<FileSystem> mock = new Mock<FileSystem> { CallBase = false };
            Mock<FileOperations> fileOperations = new Mock<FileOperations> { CallBase = false };
            Mock<DirectoryOperations> directoryOperations = new Mock<DirectoryOperations> { CallBase = false };
            
            mock.Setup(m => m.File).Returns(fileOperations.Object);
            mock.Setup(m => m.Directory).Returns(directoryOperations.Object);

            return mock;
        }

    }
}