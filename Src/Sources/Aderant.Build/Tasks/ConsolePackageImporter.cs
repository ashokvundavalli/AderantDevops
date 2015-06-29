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

namespace Aderant.Build.Tasks {
    public class ConsolePackageImporter : Microsoft.Build.Utilities.Task {
        [Required]
        public string EnvironmentManifestPath { get; set; }
        [Required]
        public string ConfigurationPath { get; set; }
        
        private string SourcePath { get; set; }
        private string ExpertSharePath { get; set; }
        private string PackageManagerPath { get; set; }
        /// <summary>
        /// Import the given packages.
        /// </summary>
        /// <returns>
        /// true if the task successfully executed; otherwise, false.
        /// </returns>
        public override bool Execute() {
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
                    //try/catch: log that we couldnt find it.
                }
            }
            return true;
        }

        private bool IfItsHere(string newPath, ref string storePath) {
            if (File.Exists(newPath)) {
                storePath = newPath;
                //TODO: log new match.
                return true;
            }
            return false;
        }

        private string GetAttributeValue(XAttribute attribute) {
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
                    //TODO: we couldn't find Packages in the config.xml Is your scheme correct?
                }
                
            } catch (Exception ex) {
                throw new RuntimeException("Unable to get package import values from the configuration file.", ex);
            }
        }
        
        private void ImportPackage(string path) {
            System.Diagnostics.Process importer = new System.Diagnostics.Process();
            importer.StartInfo = new ProcessStartInfo(PackageManagerPath, string.Format(@"/Import /File:""{0}"" ", path));
            importer.Start();
            importer.WaitForExit();
            //TODO: try get the response.
        }
        
    }
}
