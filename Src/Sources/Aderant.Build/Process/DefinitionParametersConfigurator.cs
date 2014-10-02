using System.Collections.Generic;
using Aderant.Build.Commands;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace Aderant.Build.Process {

    internal static class DefinitionParametersConfigurator {

        public static void ConfigureDefinitionParameters(IBuildDefinition buildDefinition, ExpertBuildConfiguration configuration) {
            // Set process parameters
            var parameters = WorkflowHelpers.DeserializeProcessParameters(buildDefinition.ProcessParameters);

            if (configuration != null) {
                parameters["ConfigurationFolderPath"] = configuration.ServerPathToModule + "/Build";
            }
            
            parameters["ToolsVersion"] = "12.0";

            SetBuildSettings(parameters);

            buildDefinition.ProcessParameters = WorkflowHelpers.SerializeProcessParameters(parameters);
        }

        private static void SetBuildSettings(IDictionary<string, object> parameters) {
            BuildSettings settings = new BuildSettings();
            parameters["BuildSettings"] = settings;

            settings.PlatformConfigurations.Add(new PlatformConfiguration("AnyCPU", "Debug"));
            settings.PlatformConfigurations.Add(new PlatformConfiguration("AnyCPU", "Release"));
        }
    }
}