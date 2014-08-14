using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Lapointe.PowerShell.MamlGenerator.Attributes;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace DependencyAnalyzer {
    [Cmdlet(VerbsCommon.New, "ExpertBuildDefinition")]
    [CmdletDescription("Creates a new build definition in TFS for the current module.")]
    public sealed class NewExpertBuildDefinition : PSCmdlet {

        [Parameter(HelpMessage = "The module name to create a build definition for.")]
        public string ModuleName {
            get;
            set;
        }

        [Parameter(HelpMessage = "The drop location to which the build artifacts are placed.")]
        public string DropLocation {
            get;
            set;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchPath(null, SessionState);
            string branchName = ParameterHelper.GetBranchName(SessionState);
            string modulePath = null;
            if (string.IsNullOrEmpty(ModuleName)) {
                modulePath = ParameterHelper.GetCurrentModulePath(null, SessionState);
                ModuleName = ParameterHelper.GetCurrentModuleName(null, SessionState);
            } else {
                modulePath = Path.Combine(Path.Combine(branchPath, "Modules"), ModuleName);
            }

            if (modulePath.IndexOf(modulePath, StringComparison.OrdinalIgnoreCase) < 0) {
                throw new PSInvalidOperationException(string.Format("Current branch path {0} does not contain the current module path {1}", branchPath, modulePath));
            }

            TfsTeamProjectCollection project = TeamFoundation.GetTeamProjectServer();
            var buildServer = project.GetService<IBuildServer>();

            string serverPathToModule = GetServerPathForModule(modulePath, project);
            var buildInfrastructurePath = GetServerPathForModule(Path.Combine(Path.Combine(branchPath, "Modules"), "Build.Infrastructure"), project);

            var buildConfiguration = new ExpertBuildConfiguration(branchName) {
                ModuleName = ModuleName,
                TeamProject = TeamFoundation.TeamProject,
                ServerPathToModule = serverPathToModule,
                BuildInfrastructurePath = buildInfrastructurePath,
                DropLocation = ParameterHelper.GetDropPath(null, SessionState)
            };

            IBuildDefinition existingDefinition;
            Host.UI.WriteLine("Checking for existing build for " + ModuleName);
            if (CheckForExistingBuild(buildConfiguration, buildServer, out existingDefinition)) {
                WriteObject("A build definition for the given module already exists.");
                return;
            }

            CreateBuildDefinition(buildConfiguration, buildServer);
        }

        private void CreateBuildDefinition(ExpertBuildConfiguration configuration, IBuildServer buildServer) {
            Host.UI.WriteLine("Creating new build for " + ModuleName);

            Host.UI.WriteLine("ModuleName: " + configuration.ModuleName);
            Host.UI.WriteLine("TeamProject: " + configuration.TeamProject);
            Host.UI.WriteLine("ServerPathToModule: " + configuration.ServerPathToModule);
            Host.UI.WriteLine("BuildInfrastructurePath: " + configuration.BuildInfrastructurePath);
            Host.UI.WriteLine("DropLocation: " + configuration.DropLocation);

            IBuildDefinition buildDefinition = buildServer.CreateBuildDefinition(TeamFoundation.TeamProject);
            buildDefinition.Name = configuration.BuildName;

            // Trigger type
            buildDefinition.ContinuousIntegrationType = ContinuousIntegrationType.Individual;

            // Workspace 
            buildDefinition.Workspace.AddMapping(configuration.ServerPathToModule, "$(SourceDir)", WorkspaceMappingType.Map);
            buildDefinition.Workspace.AddMapping(configuration.BuildInfrastructurePath, @"$(SourceDir)\Build\Build.Infrastructure", WorkspaceMappingType.Map);

            var controller = SetController(configuration, buildServer);

            buildDefinition.BuildController = controller;
            buildDefinition.DefaultDropLocation = configuration.DropLocation;

            IProcessTemplate upgradeTemplate = buildServer.QueryProcessTemplates(buildDefinition.TeamProject).First(p => p.TemplateType == ProcessTemplateType.Upgrade);
            buildDefinition.Process = upgradeTemplate;

            //Set process parameters
            var process = WorkflowHelpers.DeserializeProcessParameters(buildDefinition.ProcessParameters);
            process.Add("ConfigurationFolderPath", configuration.ServerPathToModule + "/Build");

            buildDefinition.ProcessParameters = WorkflowHelpers.SerializeProcessParameters(process);
            buildDefinition.Save();
        }

        private static IBuildController SetController(ExpertBuildConfiguration configuration, IBuildServer buildServer) {
            // Build Defaults
            IBuildController[] controllers = buildServer.QueryBuildControllers();
            controllers = controllers.Where(c => c.Agents.Count > 1).ToArray();
            Random random = new Random(configuration.GetHashCode());
            IBuildController controller = controllers[random.Next(controllers.Length)];
            return controller;
        }

        private bool CheckForExistingBuild(ExpertBuildConfiguration buildConfiguration, IBuildServer buildServer, out IBuildDefinition existingDefinition) {
            IBuildDefinitionSpec spec = buildServer.CreateBuildDefinitionSpec(buildConfiguration.TeamProject);
            IBuildDefinitionQueryResult result = buildServer.QueryBuildDefinitions(spec);

            foreach (IBuildDefinition buildDefinition in result.Definitions) {
                if (buildDefinition.Workspace.Mappings.Any(m => m.ServerItem.Equals(buildConfiguration.ServerPathToModule, StringComparison.OrdinalIgnoreCase))) {
                    existingDefinition = buildDefinition;
                    return true;
                }
            }

            existingDefinition = null;
            return false;
        }

        private string GetServerPathForModule(string path, TfsTeamProjectCollection project) {
            var workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();
            foreach (WorkspaceInfo info in workspaceInfo) {
                Workspace workspace = info.GetWorkspace(project);

                string serverPath = workspace.TryGetServerItemForLocalItem(path);
                if (!string.IsNullOrEmpty(serverPath)) {
                    return serverPath;
                }
            }

            throw new PSInvalidOperationException("Unable to get server path for loca path: " + path);
        }
    }

    internal sealed class ExpertBuildConfiguration {
        private readonly string branchName;

        public ExpertBuildConfiguration(string branchName) {
            this.branchName = branchName.Replace("\\", ".");
        }

        public string TeamProject {
            get;
            set;
        }

        public string ModuleName {
            get;
            set;
        }

        public string ServerPathToModule {
            get;
            set;
        }

        public string BuildInfrastructurePath {
            get;
            set;
        }

        public string BuildName {
            get { return string.Concat(branchName, ".", ModuleName); }
        }

        public string DropLocation {
            get;
            set;
        }
    }
}