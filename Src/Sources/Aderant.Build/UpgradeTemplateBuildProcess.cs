using System;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.Tasks;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace Aderant.Build {

    /// <summary>
    /// Encapsulates the UpgradeTemplate.xaml Build Process
    /// </summary>
    internal class UpgradeTemplateBuildProcess : IBuildProcessTemplate {

        public void ConfigureDefinition(ExpertBuildConfiguration configuration, IBuildServer buildServer, IBuildDefinition buildDefinition) {
            var upgradeTemplates = GetUpgradeTemplate(buildServer, buildDefinition);

            buildDefinition.Process = upgradeTemplates;

            IDictionary<string, object> parameters = WorkflowHelpers.DeserializeProcessParameters(buildDefinition.ProcessParameters);
            
            ConfigureCore(configuration, parameters);

            buildDefinition.ProcessParameters = WorkflowHelpers.SerializeProcessParameters(parameters);
        }

        public void AddProjectNodes(IBuildDetail buildDetail) {
            if (!string.IsNullOrEmpty(buildDetail.BuildDefinition.ProcessParameters)) {

                var parameters = WorkflowHelpers.DeserializeProcessParameters(buildDetail.BuildDefinition.ProcessParameters);

                if (parameters.ContainsKey("ConfigurationFolderPath")) {

                    string folderPath = parameters["ConfigurationFolderPath"] as string;
                    if (folderPath != null) {

                        string project = Path.Combine(folderPath, "TFSBuild.proj");

                        project = project.Replace(Path.DirectorySeparatorChar, '/');

                        IList<IBuildProjectNode> nodes = new List<IBuildProjectNode>();

                        nodes.Add(buildDetail.Information.AddBuildProjectNode("Release", "", "Any CPU", project, DateTime.Now, "default"));
                        nodes.Add(buildDetail.Information.AddBuildProjectNode("Debug", "", "Any CPU", project, DateTime.Now, "default"));

                        foreach (IBuildProjectNode node in nodes) {
                            node.Save();
                        }

                        buildDetail.Information.Save();
                    }
                }
            }
        }

        private static IProcessTemplate GetUpgradeTemplate(IBuildServer buildServer, IBuildDefinition buildDefinition) {
            IProcessTemplate[] upgradeTemplates = buildServer.QueryProcessTemplates(buildDefinition.TeamProject, new ProcessTemplateType[] { ProcessTemplateType.Upgrade });

            if (upgradeTemplates.Length == 0) {
                throw new InvalidOperationException("No ProcessTemplates aviable with type: " + ProcessTemplateType.Upgrade);
            }
            return upgradeTemplates[0];
        }

        private static void ConfigureCore(ExpertBuildConfiguration configuration, IDictionary<string, object> parameters) {
            if (configuration != null) {
                parameters["ConfigurationFolderPath"] = configuration.SourceControlPathToModule.TrimEnd('/') + "/Build";
            }

            BuildSettings settings = new BuildSettings();
            parameters["BuildSettings"] = settings;

            settings.PlatformConfigurations.Add(new PlatformConfiguration("Any CPU", "Debug"));
            settings.PlatformConfigurations.Add(new PlatformConfiguration("Any CPU", "Release"));
        }
    }
}