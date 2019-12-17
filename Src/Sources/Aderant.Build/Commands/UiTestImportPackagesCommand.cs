using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using System.IO;

namespace Aderant.Build.Commands {

    [Cmdlet("UITest", "ImportPackages")]
    public class UiTestImportPackagesCommand : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0, HelpMessage = "The path to the uitest automation configuration.xml")]
        public string ConfigurationPath { get; set; }

        [Parameter(Mandatory = false, Position = 1, HelpMessage = "The environment manifest which contains the sql server details.")]
        public string EnvironmentManifestPath { get; set; }
        
        protected override void ProcessRecord() {
            base.ProcessRecord();
            if (string.IsNullOrWhiteSpace(EnvironmentManifestPath)) {
                if (File.Exists("C:\\Expertshare\\environment.xml")) {
                    EnvironmentManifestPath = "C:\\Expertshare\\environment.xml";
                } else {
                    EnvironmentManifestPath = Path.Combine(ParameterHelper.GetBranchBinariesDirectory(this.SessionState), "environment.xml");
                    if (!File.Exists(EnvironmentManifestPath)) {
                        throw new FileNotFoundException("Unable to find the environment manifest", EnvironmentManifestPath);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(ConfigurationPath)) {
                ConfigurationPath = Path.Combine(ParameterHelper.GetBranchModulesDirectory(null, this.SessionState), "Tests.UIAutomation\\bin\\Test\\Resources\\configuration.xml");
                if (!File.Exists(ConfigurationPath)) {
                    throw new FileNotFoundException("Unable to find the configuration file", ConfigurationPath);
                }
            }
            ConsolePackageImporterExecutor importer = new ConsolePackageImporterExecutor(new PowerShellLogger(this.Host));
            importer.Execute(EnvironmentManifestPath, ConfigurationPath);
        }
    }
}
