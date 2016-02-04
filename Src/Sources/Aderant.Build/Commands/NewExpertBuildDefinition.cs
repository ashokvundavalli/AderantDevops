using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using Aderant.Build.DependencyAnalyzer;
using Lapointe.PowerShell.MamlGenerator.Attributes;
using Microsoft.TeamFoundation.Build.Client;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.New, "ExpertBuildDefinition")]
    [CmdletDescription("Creates a new build definition in TFS for the current module.")]
    public sealed class NewExpertBuildDefinition : PSCmdlet {

        private static string[] visualStudioVersions = new string[] {
            //"VS140COMNTOOLS",
            "VS120COMNTOOLS",
            "VS110COMNTOOLS" //C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\PrivateAssemblies
        };

        static NewExpertBuildDefinition() {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            foreach (var visualStudioVersion in visualStudioVersions) {
                string commonTools = Environment.GetEnvironmentVariable(visualStudioVersion);

                if (!string.IsNullOrEmpty(commonTools)) {
                    string privateAssemblies = Path.GetFullPath(Path.Combine(commonTools, @"..\IDE\PrivateAssemblies"));
                    
                    if (Directory.Exists(privateAssemblies)) {
                        string assemblyFileName = args.Name.Split(',')[0];
                        assemblyFileName = assemblyFileName + ".dll";

                        assemblyFileName = Path.Combine(privateAssemblies, assemblyFileName);
                        if (File.Exists(assemblyFileName)) {
                            return Assembly.LoadFrom(assemblyFileName);
                        }
                    }
                }
                return null;
            }
            return null;
        }

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

            string serverPathToModule = GetServerPathForModule(modulePath);
            string buildInfrastructurePath = GetServerPathForModule(Path.Combine(Path.Combine(branchPath, "Modules"), "Build.Infrastructure"));

            var buildConfiguration = new ExpertBuildConfiguration(branchName) {
                ModuleName = ModuleName,
                TeamProject = TeamFoundationHelper.TeamProject,
                SourceControlPathToModule = serverPathToModule,
                BuildInfrastructurePath = buildInfrastructurePath,
                DropLocation = ParameterHelper.GetDropPath(null, SessionState)
            };

            IBuildDefinition existingDefinition;

            Host.UI.WriteLine("ModuleName: " + buildConfiguration.ModuleName);
            Host.UI.WriteLine("TeamProject: " + buildConfiguration.TeamProject);
            Host.UI.WriteLine("ServerPathToModule: " + buildConfiguration.SourceControlPathToModule);
            Host.UI.WriteLine("BuildInfrastructurePath: " + buildConfiguration.BuildInfrastructurePath);
            Host.UI.WriteLine("DropLocation: " + buildConfiguration.DropLocation);

            var buildPublisher = new BuildDetailPublisher(TeamFoundationHelper.TeamFoundationServerUri, TeamFoundationHelper.TeamProject);
            IBuildServer buildServer = (IBuildServer)buildPublisher.TeamFoundationServiceFactory.GetService(typeof(IBuildServer));

            if (CheckForExistingBuild(buildConfiguration, buildServer, out existingDefinition)) {
                IBuildDefinition definition = buildPublisher.CreateBuildDefinition(buildConfiguration);
                Host.UI.WriteLine(string.Format("Updated build definition with the name [{0}] for the given module {1}.", definition.Name, ModuleName));
            } else {
                IBuildDefinition definition = buildPublisher.CreateBuildDefinition(buildConfiguration);
                Host.UI.WriteLine(string.Format("Creating new build definition with the name [{0}] for the given module {1}.", definition.Name, ModuleName));
            }

            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
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

        private string GetServerPathForModule(string path) {
            var workspace = TeamFoundationHelper.GetWorkspaceForItem(path);

                string serverPath = workspace.TryGetServerItemForLocalItem(path);
                if (!string.IsNullOrEmpty(serverPath)) {
                    return serverPath;
                }

            throw new PSInvalidOperationException("Unable to get server path for local path: " + path);
        }
    }
}