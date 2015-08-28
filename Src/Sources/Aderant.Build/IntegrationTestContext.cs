using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Aderant.Build {
    internal class IntegrationTestContext {
        private IList<string> temporaryItems = new List<string>();
        public XDocument Environment { get; private set; }

        public IntegrationTestContext(XDocument environment) {
            Environment = environment;
            NetworkShare = environment.Root.Attribute("networkSharePath").Value;
        }

        /// <summary>
        /// Gets a value indicating whether the app.config in the module should be updated with the environment details.
        /// </summary>
        public bool UpdateSolutionAppConfig {
            get {
                return !string.IsNullOrEmpty(ModulePath);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the an assembly binding section should be added to the app.config.
        /// </summary>
        /// <value>
        /// <c>true</c> if [should configure assembly binding]; otherwise, <c>false</c>.
        /// </value>
        public bool ShouldConfigureAssemblyBinding {
            get {
                return !string.IsNullOrEmpty(TestAssemblyDirectory) && !string.IsNullOrEmpty(ModulePath);
            }
        }

        public string NetworkShare { get; private set; }

        public string ModulePath { get; set; }

        public string[] TestAssemblies { get; set; }

        public string TestAssemblyDirectory { get; set; }
        public XDocument AppConfigTemplate { get; set; }

        public void AddTemporaryItem(string item) {
            this.temporaryItems.Add(item);
        }

        public void RemoveTemporaryItems() {
            foreach (string item in temporaryItems) {
                FileAttributes attr = File.GetAttributes(item);

                if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                    if (Directory.Exists(item)) {
                        Directory.Delete(item);
                    }
                } else {
                    if (File.Exists(item)) {
                        File.Delete(item);
                    }
                }
            }
        }
    }
}