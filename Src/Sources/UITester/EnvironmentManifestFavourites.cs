using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace UITester {
    internal class EnvironmentManifestFavourites {
        private bool loaded = false;
        private string databaseName;
        private string databaseServer;
        private string databaseInstance;
        private string sharePath;
        private string sourcePath;
        private string serverName;
        public string DatabaseName { get { return PopulateProperties() ?? databaseName; } }
        public string DatabaseServer { get { return PopulateProperties() ?? databaseServer; } }
        public string DatabaseInstance { get { return PopulateProperties() ?? databaseInstance; } }
        public string SharePath { get { return PopulateProperties() ?? sharePath; } }
        public string SourcePath { get { return PopulateProperties() ?? sourcePath; } }
        public string ServerName { get { return PopulateProperties() ?? serverName; } }
        public string DatabaseServerInstance {
            get { return string.IsNullOrWhiteSpace(DatabaseInstance) ? DatabaseServer : string.Concat(DatabaseServer, "\\", DatabaseInstance); }
        }

        private string environmentManifestPath;
        public string EnvironmentManifestPath {
            get { return environmentManifestPath; }
            set {
                ClearProperties();
                environmentManifestPath = value;
            }
        }

        private void ClearProperties() {
            loaded = false;
            Environment = null;
            databaseName = null;
            databaseServer = null;
            databaseInstance = null;
            sharePath = null;
            sourcePath = null;
            serverName = null;
        }

        public XDocument Environment { get; private set; }

        private string PopulateProperties() {
            if (loaded) {
                return null;
            }
            Environment = XDocument.Load(EnvironmentManifestPath);
            XElement xml = Environment.Descendants("environment").FirstOrDefault();
            
            XElement expertDatabaseServer = xml.Descendants("expertDatabaseServer").FirstOrDefault();
            XElement databaseConnection = expertDatabaseServer.Descendants("databaseConnection").FirstOrDefault();
            XAttribute databaseServerAttribute = expertDatabaseServer.Attribute("serverName");
            XAttribute databaseNameAttribute = databaseConnection.Attribute("databaseName");
            XAttribute databaseInstanceAttribute = expertDatabaseServer.Attribute("serverInstance");
            databaseName = databaseNameAttribute.Value;
            databaseServer = databaseServerAttribute.Value;
            databaseInstance = databaseInstanceAttribute.Value;

            XAttribute sharePathAttribute = xml.Attribute("networkSharePath");
            XAttribute sourcePathAttribute = xml.Attribute("sourcePath");
            sharePath = sharePathAttribute.Value;
            sourcePath = sourcePathAttribute.Value;
            try {
                serverName = xml.Descendants("servers").First().Descendants("server").First().Attribute("name").Value;
            } catch (Exception) {}
            loaded = true;
            return null;
        }

        internal EnvironmentManifestFavourites(string environmentManifestPath) {
            EnvironmentManifestPath = environmentManifestPath;
        }

    }
}
