using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Commands;

namespace Aderant.Build {

    internal sealed class IntegrationTestConfigBuilder {
        private XDocument environmentManifest;
        private string server, db;
        private bool isTest;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationTestConfigBuilder" /> class.
        /// </summary>
        /// <param name="environmentManifest">The environment manifest.</param>
        /// <param name="isTest">if set to <c>true</c> [is test].</param>
        public IntegrationTestConfigBuilder(string environmentManifest, bool isTest = false) {
            this.environmentManifest = GetDocument(environmentManifest);
            this.isTest = isTest;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationTestConfigBuilder"/> class.
        /// </summary>
        /// <param name="environment">The environment.</param>
        public IntegrationTestConfigBuilder(XDocument environment) {
            this.environmentManifest = environment;
            isTest = true;
        }

        private XDocument GetDocument(string manifest) {
            if (File.Exists(manifest)) {
                return ReadXmlFile(manifest);
            }

            return XDocument.Parse(manifest);
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
        /// <param name="integrationTestConfigTemplate">The integration test configuration template.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">Could not find instance.config under path:  + localExpertPath</exception>
        public XDocument BuildAppConfig(IntegrationTestContext testContext, string integrationTestConfigTemplate) {
            XDocument template = GetDocument(integrationTestConfigTemplate);

            XAttribute attribute = environmentManifest.Element("environment").Attribute("networkSharePath");
            string networkSharePath = attribute.Value;
            string localExpertPath = environmentManifest.Root.Descendants("server").First().Attribute("expertPath").Value;

            var serverInstanceConfiguration = Directory.GetFileSystemEntries(localExpertPath, "instance.config", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(serverInstanceConfiguration)) {
                throw new FileNotFoundException("Could not find instance.config under path: " + localExpertPath);
            }

            string appendTest = "";
            
            if (isTest) {
                appendTest = "Test";
                XDocument backup = XDocument.Load(serverInstanceConfiguration);

                string instanceConfigBackup = serverInstanceConfiguration + ".bak";
                backup.Save(instanceConfigBackup);
                testContext.AddTemporaryItem(instanceConfigBackup);

                this.BackupInstanceFile = instanceConfigBackup;
                this.InstanceFile = serverInstanceConfiguration;
            }

            var instanceConfiguration = SetRepository(networkSharePath, appendTest, serverInstanceConfiguration);

            XDocument clientsConfiguration = ReadXmlFile(Path.Combine(networkSharePath, "clients.config"));

            string hostName = GetEnvironmentHostName(instanceConfiguration);

            ReplaceInstanceSection(template, instanceConfiguration);
            ReplaceClientSection(template, clientsConfiguration, hostName);

            AddConnectionString(template, instanceConfiguration);

            return template;
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

        private XDocument SetRepository(string networkSharePath, string appendTest, string serverInstanceConfiguration) {
            XDocument instanceConfiguration = ReadXmlFile(Path.Combine(networkSharePath, "instance.config"));

            XElement repository = instanceConfiguration.Root.Descendants("repository").FirstOrDefault();
            if (repository != null) {
                XAttribute repositoryName = repository.Attribute("name");

                this.db = repositoryName.Value;

                repositoryName.Value = repositoryName.Value + appendTest;

                instanceConfiguration.Save(serverInstanceConfiguration);
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

        private void AddConnectionString(XDocument template, XDocument instanceConfiguration) {
            string connectionString = CreateConnectionString(instanceConfiguration);

            XElement configuration = template.Element("configuration");
            if (configuration != null) {
                XElement connectionStrings = configuration.Element("connectionStrings");
                if (connectionStrings != null) {
                    connectionStrings.Add(new XElement("add",
                        new XAttribute("name", "test"),
                        new XAttribute("connectionString", connectionString)));
                }
            }
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

        private void ReplaceClientSection(XDocument template, XDocument clientsConfiguration, string hostName) {
            XElement configuration = template.Element("configuration");
            if (configuration != null) {
                XElement serviceModel = configuration.Element("system.serviceModel");
                if (serviceModel != null) {
                    XElement metadataSection = serviceModel.Element("client");

                    if (metadataSection != null) {
                        SetServicePrincipalName(metadataSection, "ReportingService", hostName, "HOST");

                        clientsConfiguration.Root.Add(metadataSection.Descendants("endpoint"));

                        metadataSection.ReplaceWith(clientsConfiguration.Root);
                    }
                }
            }
        }

        private void SetServicePrincipalName(XElement metadataSection, string endPointName, string host, string type) {
            IEnumerable<XElement> elements = metadataSection.Elements("endpoint");
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

        private void ReplaceInstanceSection(XDocument template, XDocument instanceConfiguration) {
            XElement configuration = template.Element("configuration");
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

        private static XDocument ReadXmlFile(string path) {
            if (!string.IsNullOrEmpty(path)) {
                if (File.Exists(path)) {
                    string configFileContents = File.ReadAllText(path);

                    return XDocument.Parse(configFileContents);
                }
            }
            throw new FileNotFoundException("The path " + path + " does not resolve to a file.");
        }
    }
}