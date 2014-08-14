using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace Build.Tasks {
    /// <summary>
    ///     Adds a Build Platform and Configuration to a Build Definition
    /// </summary>
    /// <remarks>
    ///     We have a hybrid TFS 2008-2012 build environment. To leverage TFS integration features such as test coverage and test result publishing we need to use
    ///     the DefaultTemplate. Since we don't use this template (instead we use the UpgradeTemplate), we need to dynamically reconfigure the build definition
    ///     with the current build configuration at build time.
    ///     If this does not happen VSTest.console.exe will fail with an error similar to "The platform "Any CPU" or flavor "Release" does not match those in the build name "1.8.5138.38919". The results will not be published."
    /// </remarks>
    public class AddPlatformConfiguration : WorkflowIntegrationTask {
        private const string BuildSettings = "BuildSettings";

        public AddPlatformConfiguration() {
            Platform = "Any CPU";
        }

        public bool IsBuildAll { get; set; }

        [Required]
        public string Configuration { get; set; }

        public string Platform { get; set; }

        public override bool ExecuteInternal() {
            AddProcessParameters();
            return true;
        }

        private void AddProcessParameters() {
            IBuildDetail buildDetail = GetBuildDetail();

            //var buildProjectNode = buildDetail.Information.AddBuildProjectNode(DateTime.Now, 
            //    Configuration, 
            //    "MySolution.sln", 
            //    Platform, 
            //    "$/project/MySolution.sln", 
            //    DateTime.Now, 
            //    "Default");

            //buildProjectNode.Save();
            //buildDetail.Information.Save();
            //buildDetail.Save();

            //buildDetail.Information.AddBuildProjectNode()
            // IConfigurationSummary summary = buildDetail.Information.AddConfigurationSummary(Configuration, Platform);
            //summary.Save();

            //System.Diagnostics.Debugger.Launch();

            if (IsBuildAll) {
                AddConfigurationSummary(buildDetail);
            }

            BuildSettings buildSettings = null;
            IDictionary<string, object> parameters = new Dictionary<string, object>();

            string processParameters = buildDetail.BuildDefinition.ProcessParameters;
            if (!string.IsNullOrEmpty(processParameters)) {
                parameters = WorkflowHelpers.DeserializeProcessParameters(processParameters);

                if (parameters.ContainsKey(BuildSettings)) {
                    buildSettings = parameters[BuildSettings] as BuildSettings;
                }
            }

            if (buildSettings == null) {
                buildSettings = new BuildSettings();
                buildSettings.PlatformConfigurations = new PlatformConfigurationList();
                buildSettings.PlatformConfigurations.Add(new PlatformConfiguration(Platform, Configuration));
            } else {
                if (buildSettings.PlatformConfigurations == null) {
                    buildSettings.PlatformConfigurations = new PlatformConfigurationList();
                }

                if (!buildSettings.PlatformConfigurations.Any(cfg => cfg.Platform == Platform && cfg.Configuration == Configuration)) {
                    buildSettings.PlatformConfigurations.Add(new PlatformConfiguration(Platform, Configuration));
                }
            }

            parameters[BuildSettings] = buildSettings;
            buildDetail.BuildDefinition.ProcessParameters = WorkflowHelpers.SerializeProcessParameters(parameters);
            buildDetail.BuildDefinition.Save();
        }

        private void AddConfigurationSummary(IBuildDetail buildDetail) {
            List<IBuildInformationNode> buildInformationNodes = buildDetail.Information.GetNodesByType("ConfigurationSummary");
            bool hasPlatform = false;
            bool hasConfiguration = false;
            foreach (IBuildInformationNode node in buildInformationNodes) {
                if (hasPlatform && hasConfiguration) {
                    break;
                }

                if (node.Fields.ContainsKey("Platform")) {
                    string s = node.Fields["Platform"];

                    hasPlatform = s != null && s == Platform;
                }

                if (node.Fields.ContainsKey("Flavor")) {
                    string s = node.Fields["Flavor"];

                    hasConfiguration = s != null && s == Configuration;
                }
            }

            if (!hasPlatform && !hasConfiguration) {
                IConfigurationSummary summary = buildDetail.Information.AddConfigurationSummary(Configuration, Platform);
                summary.Save();
                buildDetail.Information.Save();
            }
        }
    }
}