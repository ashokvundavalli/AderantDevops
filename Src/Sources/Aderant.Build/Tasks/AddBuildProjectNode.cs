using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Common;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Tasks {
    public sealed class AddBuildProjectNode : Task {
        [Required]
        public string TeamFoundationServerUri { get; set; }

        [Required]
        public string TeamProject { get; set; }

        [Required]
        public string BuildUri { get; set; }

        public string Flavor { get; set; }

        public string Platform { get; set; }

        public string ProjectFile { get; set; }

        public override bool Execute() {
            BuildDetailPublisher controller = new BuildDetailPublisher(TeamFoundationServerUri, TeamProject);

            IBuildDetail buildDetails = controller.GetBuildDetails(BuildUri);

            VersionControlServer service = (VersionControlServer)controller.TeamFoundationServiceFactory.GetService(typeof(VersionControlServer));
            Workspace workspace = service.TryGetWorkspace(ProjectFile);

            if (workspace != null) {
                string projectServerPath = workspace.TryGetServerItemForLocalItem(ProjectFile);

                if (!string.IsNullOrEmpty(projectServerPath)) {
                    List<IBuildInformationNode> projects = buildDetails.Information.GetNodesByType(InformationTypes.BuildProject, true);

                    IBuildInformationNode parentProject = TryGetParentProject(projects);
                    if (parentProject != null) {
                        Log.LogMessage("Adding project node(s) for project: " + ProjectFile);

                        IBuildProjectNode node = parentProject.Children.AddBuildProjectNode(Flavor, ProjectFile, Platform, projectServerPath, DateTime.Now, "default");

                        node.Save();
                        parentProject.Save();
                    } else {
                        Log.LogMessage("Adding project node(s) for project: " + ProjectFile);

                        IBuildProjectNode node = buildDetails.Information.AddBuildProjectNode(Flavor, ProjectFile, Platform, projectServerPath, DateTime.Now, "default");

                        node.Save();
                        parentProject.Save();
                    }
                  
                    buildDetails.Information.Save();
                }
            }

            return true;
        }

        private static IBuildInformationNode TryGetParentProject(List<IBuildInformationNode> projects) {
            IBuildInformationNode parentProject = null;
            foreach (IBuildInformationNode project in projects) {
                string serverPath;
                if (project.Fields.TryGetValue("ServerPath", out serverPath)) {
                    if (serverPath.EndsWith("/Build/TFSBuild.proj", StringComparison.OrdinalIgnoreCase)) {
                        parentProject = project;
                        break;
                    }
                }
            }
            return parentProject;
        }
    }
}