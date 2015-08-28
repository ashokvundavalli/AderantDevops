using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Host;
using System.Xml.Linq;
using Aderant.Build.Commands;
using Aderant.Build.Logging;

namespace Aderant.Build {
    internal class IntegrationTestAssemblyConfig {
        private readonly string configFile;
        private readonly FileSystem fileSystem;
        public IntegrationTestAssemblyConfig(string configFile, FileSystem fileSystem) {

            if (configFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                this.configFile = configFile + ".config";
            } else {
                this.configFile = configFile;    
            }

            this.fileSystem = fileSystem;
        }

        public static IEnumerable<IntegrationTestAssemblyConfig> Create(string directory) {
            string[] testAssemblies = Directory.GetFileSystemEntries(directory, "IntegrationTest*.dll", SearchOption.TopDirectoryOnly);

            List<IntegrationTestAssemblyConfig> assembly = new List<IntegrationTestAssemblyConfig>(testAssemblies.Length);

            foreach (string testAssembly in testAssemblies) {
                assembly.Add(new IntegrationTestAssemblyConfig(testAssembly, FileSystem.Default));
            }

            return assembly;
        }

        public string AppConfigFile {
            get; 
            private set;
        }

        public XDocument AppConfig {
            get; 
            private set;
        }

        public bool IgnoreMissingConfigFile { get; set; }

        public void ConfigureAppConfig(IntegrationTestContext testContext) {
            IntegrationTestConfigBuilder configBuilder = new IntegrationTestConfigBuilder(fileSystem, testContext.Environment);

            AppConfigFile = configFile;

            if (fileSystem.File.Exists(configFile)) {
                string configurationFileText = fileSystem.File.ReadAllText(configFile);

               
                AppConfig = configBuilder.ConfigureAppConfig(testContext, XDocument.Parse(configurationFileText));
            } else {
                if (testContext.AppConfigTemplate == null && !IgnoreMissingConfigFile) {
                    throw new InvalidOperationException("File " + AppConfigFile + " does not exist and no template was provided to produce one from.");
                }

                AppConfig = testContext.AppConfigTemplate;
            }
        }

       

        public void SaveAppConfig() {
            if (AppConfig != null) {
                if (!string.IsNullOrEmpty(AppConfigFile)) {
                    fileSystem.File.WriteAllText(AppConfigFile, AppConfig.ToString());
                }
            }
        }
    }

    internal static class LoggerFactory {
        public static ILogger CreateLogger(PSHost host) {
            return new PowerShellLogger(host);
        }
    }
}