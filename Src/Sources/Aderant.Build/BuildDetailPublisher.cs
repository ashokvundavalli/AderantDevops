using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;

namespace Aderant.Build {
    internal class BuildDetailPublisher : IDisposable {
        private readonly string teamProject;
        private readonly string teamFoundationServerUri;
        private IServiceProvider teamFoundationFactory;
        private IBuildProcessTemplate buildProcessTemplate;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildDetailPublisher"/> class.
        /// </summary>
        /// <param name="teamFoundationServerUri">The team foundation server URI.</param>
        /// <param name="teamProject">The team project.</param>
        public BuildDetailPublisher(string teamFoundationServerUri, string teamProject) {
            this.teamProject = teamProject;
            this.teamFoundationServerUri = teamFoundationServerUri;
        }

        /// <summary>
        /// Gets or sets the team foundation server factory.
        /// </summary>
        /// <value>
        /// The team foundation factory.
        /// </value>
        public IServiceProvider TeamFoundationServiceFactory {
            get {
                if (teamFoundationFactory == null) {
                    teamFoundationFactory = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(teamFoundationServerUri));
                }
                return teamFoundationFactory;
            }
            set {
                teamFoundationFactory = value;
            }
        }

        /// <summary>
        /// Gets or sets the build process template. This is the build process workflow abstraction that the build will use.
        /// </summary>
        /// <value>
        /// The build process template.
        /// </value>
        public IBuildProcessTemplate BuildProcessTemplate  {
            get { 
                if (buildProcessTemplate == null) {
                    buildProcessTemplate = new UpgradeTemplateBuildProcess();
                }
                return buildProcessTemplate;
            }
            set {
                buildProcessTemplate = value;
            }
        }

        /// <summary>
        /// Creates or gets the approperiate build definition for the provided configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public IBuildDefinition CreateBuildDefinition(ExpertBuildConfiguration configuration) {
            // Get the Build Server
            IBuildServer buildServer = (IBuildServer) TeamFoundationServiceFactory.GetService(typeof (IBuildServer));

            if (string.IsNullOrEmpty(configuration.TeamProject)) {
                configuration.TeamProject = teamProject;
            }

            IBuildDefinition build = GetOrCreateBuildDefinition(buildServer, configuration);

            build.Save();

            return build;
        }

        /// <summary>
        /// Creates a new build within TFS.
        /// </summary>
        /// <param name="buildDefinition">The build definition.</param>
        /// <param name="expertBuildDetail">The expert build detail.</param>
        /// <param name="sourceGetVersion">The source version the build uses.</param>
        public IBuildDetail CreateNewBuild(IBuildDefinition buildDefinition, ExpertBuildDetail expertBuildDetail, string sourceGetVersion) {
            IBuildDetail buildDetail = buildDefinition.CreateManualBuild(expertBuildDetail.BuildNumber, expertBuildDetail.DropLocation);

            buildDetail.Status = BuildStatus.InProgress;

            if (!string.IsNullOrEmpty(expertBuildDetail.LogLocation)) {
                buildDetail.LogLocation = expertBuildDetail.LogLocation;
            }

            // Create platform/flavor information against which test results can be published
            BuildProcessTemplate.AddProjectNodes(buildDetail);

            buildDetail.DropLocation = expertBuildDetail.DropLocation;
            buildDetail.KeepForever = false;
            buildDetail.SourceGetVersion = sourceGetVersion;

            if (expertBuildDetail.BuildSummary != null) {
                BuildSummary summary = expertBuildDetail.BuildSummary;
                ICustomSummaryInformation summaryInformation = buildDetail.Information.AddCustomSummaryInformation(summary.Message, summary.Section, "Expert Summary", 0);
                summaryInformation.Save();
            }

            buildDetail.Save();

            return buildDetail;
        }

        public void FinalizeBuild(IBuildDetail buildDetail, bool completedSuccessfully) {
            buildDetail.Status = BuildStatus.Failed;
            buildDetail.CompilationStatus = BuildPhaseStatus.Unknown;
            buildDetail.TestStatus = BuildPhaseStatus.Unknown;
            
            if (completedSuccessfully) {
                buildDetail.Status = BuildStatus.Succeeded;
                buildDetail.CompilationStatus = BuildPhaseStatus.Succeeded;
                buildDetail.TestStatus = BuildPhaseStatus.Succeeded;
            }

            buildDetail.FinalizeStatus(buildDetail.Status);
        }

        private IBuildDefinition GetOrCreateBuildDefinition(IBuildServer buildServer, ExpertBuildConfiguration configuration) {
            if (string.IsNullOrEmpty(configuration.BuildName)) {
                throw new ArgumentNullException("BuildName", "BuildName cannot be null or empty");
            }

            IBuildDefinitionSpec spec = buildServer.CreateBuildDefinitionSpec(configuration.TeamProject, configuration.BuildName);
            IBuildDefinitionQueryResult result = buildServer.QueryBuildDefinitions(spec);

            if (result == null) {
                return CreateBuildDefinition(buildServer, configuration);
            }

            if (result.Definitions.Length == 1) {
                // Found the unique match
                ConfigureAndSave(buildServer, configuration, null, result.Definitions[0]);
                return result.Definitions[0];
            }

            if (result.Definitions.Length == 0) {
                // Found no match - create new 
                return CreateBuildDefinition(buildServer, configuration);
            }

            if (result.Definitions.Length > 1) {
                throw new InvalidOperationException("Multiple build definitions found with the same name:" + configuration.BuildName);
            }

            throw new InvalidOperationException("Unknown error while attempting to configure build definitions the name:" + configuration.BuildName);
        }

        private IBuildDefinition CreateBuildDefinition(IBuildServer buildServer, ExpertBuildConfiguration configuration) {
            IBuildController controller = GetBuildController(buildServer, configuration);

            IBuildDefinition buildDefinition = buildServer.CreateBuildDefinition(teamProject);

            ConfigureAndSave(buildServer, configuration, controller, buildDefinition);

            return buildDefinition;
        }

        private void ConfigureAndSave(IBuildServer buildServer, ExpertBuildConfiguration configuration, IBuildController controller, IBuildDefinition buildDefinition) {
            ConfigureCoreProperties(configuration, controller, buildDefinition);
            ConfigureRetentionPolicy(buildDefinition);
            ConfigureWorkspaceMapping(configuration, buildDefinition);
            ConfigureBuildProcess(configuration, buildServer, buildDefinition);

            buildDefinition.Save();
        }

        private void ConfigureRetentionPolicy(IBuildDefinition buildDefinition) {
            List<IRetentionPolicy> retentionPolicyList = buildDefinition.RetentionPolicyList;

            foreach (IRetentionPolicy policy in retentionPolicyList) {
                
                if (policy.BuildReason == BuildReason.Triggered) {

                    if (policy.BuildStatus == BuildStatus.Stopped) {
                        policy.NumberToKeep = 1;
                        policy.DeleteOptions = DeleteOptions.All;
                        continue;
                    }

                    if (policy.BuildStatus == BuildStatus.Failed) {
                        policy.NumberToKeep = 5;
                        policy.DeleteOptions = DeleteOptions.All;
                        continue;
                    }

                    if (policy.BuildStatus == BuildStatus.PartiallySucceeded) {
                        policy.NumberToKeep = 2;
                        policy.DeleteOptions = DeleteOptions.All;
                        continue;
                    }

                    if (policy.BuildStatus == BuildStatus.Succeeded) {
                        policy.NumberToKeep = 5;
                        policy.DeleteOptions = DeleteOptions.All;
                    }
                }
            }
        }

        private static void ConfigureCoreProperties(ExpertBuildConfiguration configuration, IBuildController controller, IBuildDefinition buildDefinition) {
            // Build Name
            buildDefinition.Name = configuration.BuildName;

            // Trigger type
            buildDefinition.ContinuousIntegrationType = ContinuousIntegrationType.Individual;

            // Drop Location
            if (buildDefinition.DefaultDropLocation != configuration.DropLocation) {
                buildDefinition.DefaultDropLocation = configuration.DropLocation;
            }

            if (controller != null) {
                // Controller
                buildDefinition.BuildController = controller;
            }
        }

        private static void ConfigureWorkspaceMapping(ExpertBuildConfiguration configuration, IBuildDefinition buildDefinition) {
            foreach (IWorkspaceMapping mapping in buildDefinition.Workspace.Mappings.ToArray()) {
                buildDefinition.Workspace.RemoveMapping(mapping);
            }

            // Workspace 
            buildDefinition.Workspace.AddMapping(configuration.SourceControlPathToModule, "$(SourceDir)", WorkspaceMappingType.Map);
            buildDefinition.Workspace.AddMapping(configuration.BuildInfrastructurePath, @"$(SourceDir)\Build\" + BuildConstants.BuildInfrastructureDirectory, WorkspaceMappingType.Map);
        }
    
        private void ConfigureBuildProcess(ExpertBuildConfiguration configuration, IBuildServer buildServer, IBuildDefinition buildDefinition) {
            BuildProcessTemplate.ConfigureDefinition(configuration, buildServer, buildDefinition);
        }

        private IBuildController GetBuildController(IBuildServer buildServer, ExpertBuildConfiguration configuration) {
            // Build Defaults
            IBuildController[] controllers = buildServer.QueryBuildControllers();
            controllers = controllers.Where(c => c.Agents.Count > 1).ToArray();

            if (controllers.Length == 0) {
                throw new InvalidOperationException("There are no controllers with more than 1 agent or no controllers are available");
            }
            
            Random random = new Random(configuration.GetHashCode());
            IBuildController controller = controllers[random.Next(controllers.Length)];
            return controller;
        }

        public void Dispose() {
            IDisposable disposable = teamFoundationFactory as IDisposable;

            if (disposable != null) {
                disposable.Dispose();
                teamFoundationFactory = null;
                buildProcessTemplate = null;
            }
        }

        /// <summary>
        /// Gets the build details.
        /// </summary>
        /// <param name="buildUri">The build URI.</param>
        public IBuildDetail GetBuildDetails(string buildUri) {
            Uri uri = new Uri(buildUri);

            // Get the Build Server
            IBuildServer buildServer = (IBuildServer)TeamFoundationServiceFactory.GetService(typeof(IBuildServer));
            return buildServer.GetBuild(uri, null, QueryOptions.None);
        }
    }
}
