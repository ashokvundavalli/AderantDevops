using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Commands;

namespace Aderant.Build {

    internal sealed class IntegrationTestConfigBuilder {
        private readonly FileSystem fileSystem;
        private readonly XDocument environment;
        private string server, db;
        private bool isTest;

        public IntegrationTestConfigBuilder(FileSystem fileSystem, XDocument environment, bool isTest = false) {
            this.fileSystem = fileSystem;
            this.environment = environment;
            this.isTest = isTest;
        }

        public string GetServer() {
            return this.server;
        }

        public string GetDB() {
            return this.db;
        }

        public string GetTestDB() {
            if (isTest) {
                return this.db + "Test";
            } else {
                return this.db;
            }
        }

        /// <summary>
        /// Builds the integration test configuration file.
        /// </summary>
        /// <param name="testContext">The test context.</param>
        /// <param name="existingAppConfigDocument"></param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">Could not find instance.config</exception>
        public XDocument ConfigureAppConfig(IntegrationTestContext testContext, XDocument existingAppConfigDocument) {
            XAttribute attribute = environment.Element("environment").Attribute("networkSharePath");
            string networkSharePath = attribute.Value;
            string localExpertPath = environment.Root.Descendants("server").First().Attribute("expertPath").Value;

            var entries = fileSystem.Directory.GetFileSystemEntries(localExpertPath, "instance.config", SearchOption.AllDirectories);
            if (entries.Length == 0) {
                throw new FileNotFoundException("Could not find instance.config under path: " + localExpertPath);
            }

            string appendTest = "";
            
            if (isTest) {
                appendTest = "Test";
                XDocument backup = XDocument.Load(entries[0]);

                string instanceConfigBackup = entries + ".bak";
                WriteXmlFile(instanceConfigBackup, backup.ToString());
                testContext.AddTemporaryItem(instanceConfigBackup);

                this.BackupInstanceFile = instanceConfigBackup;
                this.InstanceFile = entries[0];
            }

            XDocument instanceConfiguration = ChangeDatabaseName(networkSharePath, appendTest, entries[0]);
            
            XDocument clientsConfiguration = ReadXmlFile(Path.Combine(networkSharePath, "clients.config"));

            string hostName = GetEnvironmentHostName(instanceConfiguration);

            ReplaceInstanceSection(existingAppConfigDocument, instanceConfiguration);
            ReplaceClientSection(existingAppConfigDocument, clientsConfiguration, hostName);

            if (testContext.AppConfigTemplate != null) {
                UpdateBehaviours(existingAppConfigDocument, testContext.AppConfigTemplate);
            }

            AddConnectionString(existingAppConfigDocument, instanceConfiguration);

            if (testContext.ShouldConfigureAssemblyBinding) {
                AddAssemblyBindingElement(existingAppConfigDocument);
            }

            return existingAppConfigDocument;
        }

        private void UpdateBehaviours(XDocument document, XDocument template) {
            var targetBehaviorsNode = document.Root.GetOrAddElement("system.serviceModel/behaviors/endpointBehaviors");
            var sourceBehaviorsNode = template.Root.GetOrAddElement("system.serviceModel/behaviors/endpointBehaviors");

            targetBehaviorsNode.ReplaceWith(sourceBehaviorsNode);
        }

        /// <summary>
        /// Gets the backup instance file.
        /// </summary>
        /// <value>
        /// The backup instance file.
        /// </value>
        public string BackupInstanceFile { get; private set; }

        /// <summary>
        /// Gets the instance file.
        /// </summary>
        /// <value>
        /// The instance file.
        /// </value>
        public string InstanceFile { get; private set; }

        private void AddAssemblyBindingElement(XDocument document) {
            XElement runtime = document.Element("configuration").GetOrAddElement("runtime");

            XNamespace ns = "urn:schemas-microsoft-com:asm.v1";
            XElement assemblyBinding = new XElement(ns + "assemblyBinding");
            assemblyBinding.Add(new XElement(ns + "probing", new XAttribute("privatePath", "Dependencies;Bin;NetworkShare")));

            runtime.Add(assemblyBinding);
        }

        private XDocument ChangeDatabaseName(string networkSharePath, string appendTest, string serverInstanceConfiguration) {
            XDocument instanceConfiguration = ReadXmlFile(Path.Combine(networkSharePath, "instance.config"));

            XElement repository = instanceConfiguration.Root.Descendants("repository").FirstOrDefault();
            if (repository != null) {
                XAttribute repositoryName = repository.Attribute("name");

                this.db = repositoryName.Value;

                repositoryName.Value = repositoryName.Value + appendTest;

                WriteXmlFile(serverInstanceConfiguration, instanceConfiguration.ToString());
            }

            return instanceConfiguration;
        }

        private string GetEnvironmentHostName(XDocument instanceConfiguration) {
            if (instanceConfiguration.Root != null) {
                XElement queryService = instanceConfiguration.Root.Element("queryService");

                string value = queryService.Attribute("uri").Value;
                Uri uri = new Uri(value);
                return uri.Host;
            }

            throw new InvalidOperationException("Could not get environment host name from instance.config for the integration test run.");
        }

        private void AddConnectionString(XDocument document, XDocument instanceConfiguration) {
            string connectionString = CreateConnectionString(instanceConfiguration);

            XElement configuration = document.Element("configuration");
            if (configuration != null) {
                XElement connectionStrings = configuration.Element("connectionStrings");

                if (connectionStrings != null) {
                    bool added = false;

                    foreach (var add in connectionStrings.Descendants("add")) {
                        XAttribute attribute = add.Attribute("name");

                        if (attribute != null) {
                            if (string.Equals(attribute.Value, "test", StringComparison.OrdinalIgnoreCase)) {
                                add.ReplaceWith(CreateConnectionStringElement(connectionString));

                                added = true;
                                break;
                            }
                        }
                    }

                    if (!added) {
                        connectionStrings.Add(CreateConnectionStringElement(connectionString));
                    }
                }
            }
        }

        private static XElement CreateConnectionStringElement(string connectionString) {
            return new XElement("add",
                new XAttribute("name", "test"),
                new XAttribute("connectionString", connectionString));
        }

        private string CreateConnectionString(XDocument instanceConfiguration) {
            if (instanceConfiguration.Root != null) {

                XElement repository = instanceConfiguration.Root.Element("repository");

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.InitialCatalog = repository.Attribute("name").Value;
                builder.DataSource = repository.Attribute("server").Value;
                this.server = repository.Attribute("server").Value;
                builder.UserID = "cmsdbo";
                builder.Password = "cmsdbo";

                return builder.ToString();
            }

            throw new InvalidOperationException("Could not construct a connection string for the integration test run.");
        }

        private void ReplaceClientSection(XDocument document, XDocument clientsConfiguration, string hostName) {
            XElement clientSection = document.Element("configuration").GetOrAddElement("system.serviceModel/client");

            if (clientSection != null) {
                //SetServicePrincipalName(clientSection, "ReportingService", hostName, "HOST");
                //clientsConfiguration.Root.Add(clientSection.Descendants("endpoint"));

                clientSection.ReplaceWith(clientsConfiguration.Root);
            }
        }

        private void SetServicePrincipalName(XElement document, string endPointName, string host, string type) {
            IEnumerable<XElement> elements = document.Elements("endpoint");
            foreach (XElement element in elements) {
                if (element.Attribute("name").Value == endPointName) {
                    XElement identity = element.Element("identity");
                    if (identity != null) {
                        XElement servicePrincipalName = identity.Element("servicePrincipalName");
                        if (servicePrincipalName != null) {
                            servicePrincipalName.Attribute("value").SetValue(type + "/" + host);
                        }
                    }
                    break;
                }
            }
        }

        private void ReplaceInstanceSection(XDocument document, XDocument instanceConfiguration) {
            XElement configuration = document.Element("configuration");
            if (configuration != null) {
                XElement aderantSection = configuration.Element("aderant");
                if (aderantSection != null) {
                    XElement metadataSection = aderantSection.Element("instanceMetadataConfigurationSection");

                    if (metadataSection != null) {
                        metadataSection.ReplaceWith(instanceConfiguration.Root);
                    }
                }
            }
        }

        private XDocument ReadXmlFile(string path) {
            if (!string.IsNullOrEmpty(path)) {
                if (fileSystem.File.Exists(path)) {
                    string configFileContents = fileSystem.File.ReadAllText(path);

                    return XDocument.Parse(configFileContents);
                }
            }
            throw new FileNotFoundException("The path " + path + " does not resolve to a file.");
        }

        private void WriteXmlFile(string path, string contents) {
            fileSystem.File.WriteAllText(path, contents);
        }
    }
}