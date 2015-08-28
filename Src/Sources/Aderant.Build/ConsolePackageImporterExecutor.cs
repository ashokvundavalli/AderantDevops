using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Management.Automation;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build {
    public class ConsolePackageImporterExecutor {
        public ConsolePackageImporterExecutor(Aderant.Build.Logging.ILogger logger) {
            Logger = logger;
        }

        private string EnvironmentManifestPath { get; set; }
        private string ConfigurationPath { get; set; }
        private string SourcePath { get; set; }
        private string ExpertSharePath { get; set; }
        private string PackageManagerPath { get; set; }
        
        /// <summary>
        /// Import the given packages.
        /// </summary>
        /// <returns>
        /// true if the task successfully executed; otherwise, false.
        /// </returns>
        public bool Execute(string environmentManifestPath, string configurationPath) {
            EnvironmentManifestPath = environmentManifestPath;
            ConfigurationPath = configurationPath;

            GetValuesFromEnvironment(EnvironmentManifestPath);
            List<string> packages = new List<string>();
            List<string> tryLocations = new List<string> { Path.Combine(SourcePath, "Packages") };
            GetValuesFromConfiguration(ConfigurationPath, packages, tryLocations);
            PackageManagerPath = Path.Combine(ExpertSharePath, "PackageManagerConsole.exe");
            foreach (string item in packages) {
                string packagePath = item;
                bool resolved = false;
                
                if (File.Exists(packagePath)) {
                    resolved = true;
                } else {
                    foreach (string location in tryLocations) {
                        if (IfItsHere(Path.Combine(location, packagePath), ref packagePath)) {
                            resolved = true;
                            break;
                        }
                    }
                }
                if (resolved) {
                    ImportPackage(packagePath);
                } else {
                    LogWarning("Unable to find the package: {0} in any of the given locations", packagePath);
                }
            }
            return true;
            
        }

        private bool IfItsHere(string newPath, ref string storePath) {
            if (File.Exists(newPath)) {
                storePath = newPath;
                LogDebug("We found the package file at: {0}", newPath);
                return true;
            }
            return false;
        }

        private static string GetAttributeValue(XAttribute attribute) {
            return attribute != null ? attribute.Value : null;
        }

        private void GetValuesFromEnvironment(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentNullException("EnvironmentManifestPath");
            }
            try {
                XDocument xml = XDocument.Load(manifestPath);
                XElement environment = xml.Descendants("environment").First();
                XAttribute sourcePathAttribute = environment.Attributes("sourcePath").FirstOrDefault();
                SourcePath = GetAttributeValue(sourcePathAttribute);
                XAttribute sharePathAttribute = environment.Attributes("networkSharePath").FirstOrDefault();
                ExpertSharePath = GetAttributeValue(sharePathAttribute);
            } catch (Exception ex) {
                throw new RuntimeException("Could not find the xml elements we were looking for. Is the schema correct?", ex);
            }
        }

        private void GetValuesFromConfiguration(string xmlPath, List<string> packages, List<string> locations) {
            if (string.IsNullOrWhiteSpace(xmlPath)) {
                throw new ArgumentNullException("ConfigurationPath");
            }
            try {
                XDocument xml = XDocument.Load(xmlPath);
                XElement configuration = xml.Descendants("UITestConfiguration").First();
                XElement packagesElement = configuration.Descendants("Packages").FirstOrDefault();
                if (packagesElement != null) {
                    IEnumerable<XElement> imports = packagesElement.Descendants("Import");
                    packages.AddRange(imports.Select(item => item.Value));
                    IEnumerable<XElement> searchLocations = packagesElement.Descendants("Location");
                    locations.AddRange(searchLocations.Select(item => item.Value));
                } else {
                    LogWarning("Unable to find \"Packages\" in the config file: {0} Is your scheme correct?", xmlPath);
                }
            } catch (IOException ex) {
                throw new RuntimeException("Unable to import packages. Could not access the configuration file", ex);
            } catch (Exception ex) {
                throw new RuntimeException("Unable to get package import values from the configuration file.", ex);
            }
        }
        
        private void ImportPackage(string packageFilePath) {
            System.Diagnostics.Process importer = new System.Diagnostics.Process();
            importer.StartInfo = new ProcessStartInfo(PackageManagerPath, string.Format(@"/Import /File:""{0}"" ", packageFilePath));
            importer.Start();
            importer.WaitForExit();
            //We could try get the response.
        }

        #region Logging
        private Aderant.Build.Logging.ILogger Logger { get; set; }
        private bool HasLogger { get { return Logger != null; } }
        
        private void LogDebug(string message, params string[] args) {
            if (HasLogger) {
                Logger.Debug(message, args);
            }
        }

        private void LogError(string message, params string[] args) {
            if (HasLogger) {
                Logger.Error(message, args);
            }
        }

        private void LogMessage(string message, params string[] args) {
            if (HasLogger) {
                Logger.Log(message, args);
            }
        }

        private void LogWarning(string message, params string[] args) {
            if (HasLogger) {
                Logger.Warning(message, args);
            }
        }
        #endregion Logging
    }
}
