using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;
using Lapointe.PowerShell.MamlGenerator.Attributes;
using Microsoft.TeamFoundation.Build.Client;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.New, "ExpertBuildDefinition")]
    [CmdletDescription("Creates a new build definition in TFS for the current module.")]
    public sealed class NewExpertBuildDefinition : PSCmdlet {

        [Parameter(HelpMessage = "The module name to create a build definition for.")]
        public string ModuleName {
            get;
            set;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchPath(null, SessionState);
            string branchName = ParameterHelper.GetBranchName(SessionState);
            string modulePath;

            if (string.IsNullOrEmpty(ModuleName)) {
                modulePath = ParameterHelper.GetCurrentModulePath(null, SessionState);
                ModuleName = ParameterHelper.GetCurrentModuleName(null, SessionState);
            } else {
                modulePath = Path.Combine(Path.Combine(branchPath, "Modules"), ModuleName);
            }

            if (modulePath.IndexOf(modulePath, StringComparison.OrdinalIgnoreCase) < 0) {
                throw new PSInvalidOperationException(string.Format("Current branch path {0} does not contain the current module path {1}", branchPath, modulePath));
            }

            var workspace = ServiceLocator.GetInstance<ITeamFoundationWorkspace>();

            string serverPathToModule = workspace.TryGetServerItemForLocalItem(modulePath);

            var buildConfiguration = new ExpertBuildConfiguration(branchName) {
                ModuleName = ModuleName,
                TeamProject = workspace.TeamProject,
                SourceControlPathToModule = serverPathToModule,
                DropLocation = ParameterHelper.GetDropPath(null, SessionState)
            };

            IBuildDefinition existingDefinition;

            Host.UI.WriteLine("ModuleName: " + buildConfiguration.ModuleName);
            Host.UI.WriteLine("TeamProject: " + buildConfiguration.TeamProject);
            Host.UI.WriteLine("ServerPathToModule: " + buildConfiguration.SourceControlPathToModule);
            Host.UI.WriteLine("DropLocation: " + buildConfiguration.DropLocation);

            var buildPublisher = new BuildDetailPublisher(workspace.ServerUri, workspace.TeamProject);
            IBuildServer buildServer = (IBuildServer)buildPublisher.TeamFoundationServiceFactory.GetService(typeof(IBuildServer));

            if (CheckForExistingBuild(buildConfiguration, buildServer, out existingDefinition)) {
                IBuildDefinition definition = buildPublisher.CreateBuildDefinition(buildConfiguration);
                Host.UI.WriteLine(string.Format("Updated build definition with the name [{0}] for the given module {1}.", definition.Name, ModuleName));
            } else {
                IBuildDefinition definition = buildPublisher.CreateBuildDefinition(buildConfiguration);
                Host.UI.WriteLine(string.Format("Creating new build definition with the name [{0}] for the given module {1}.", definition.Name, ModuleName));
            }
        }

        private bool CheckForExistingBuild(ExpertBuildConfiguration buildConfiguration, IBuildServer buildServer, out IBuildDefinition existingDefinition) {
            Host.UI.WriteLine("Checking for existing build for " + buildConfiguration.ModuleName);

            IBuildDefinitionSpec spec = buildServer.CreateBuildDefinitionSpec(buildConfiguration.TeamProject);
            IBuildDefinitionQueryResult result = buildServer.QueryBuildDefinitions(spec);

            foreach (IBuildDefinition buildDefinition in result.Definitions) {
                if (buildDefinition.Workspace.Mappings.Any(m => m.ServerItem.Equals(buildConfiguration.SourceControlPathToModule, StringComparison.OrdinalIgnoreCase))) {
                    existingDefinition = buildDefinition;
                    return true;
                }
            }

            existingDefinition = null;
            return false;
        }
    }
}