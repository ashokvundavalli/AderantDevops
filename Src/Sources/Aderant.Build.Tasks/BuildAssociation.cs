using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Aderant.Build.Tasks {
    public class BuildAssociation {
        private readonly ILogger logger;
        private VssConnection connection;

        public BuildAssociation(ILogger logger, VssConnection connection) {
            this.logger = logger;
            this.connection = connection;
        }

        public async Task AssociateWorkItemsToBuildAsync(string teamProject, int buildId) {
            var buildHttpClient = connection.GetClient<BuildHttpClient>();
            var workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();

            var buildDetail = await buildHttpClient.GetBuildAsync(buildId);

            var workItemReferences = await buildHttpClient.GetBuildWorkItemsRefsAsync(teamProject, buildId);

            foreach (var result in workItemReferences) {
                logger.Info("Associating {0} to build {1}" + result.Id, buildDetail.BuildNumber);

                var patch = new JsonPatchDocument {
                    new JsonPatchOperation {
                        Operation = Operation.Add,
                        Path = "/fields/Microsoft.VSTS.Build.IntegrationBuild",
                        Value = buildDetail.BuildNumber
                    }
                };

                await workItemClient.UpdateWorkItemAsync(patch, Int32.Parse(result.Id));
            }
        }

        public void AssociateWorkItemsToBuild(string teamProject, int buildId) {
            try {
                AssociateWorkItemsToBuildAsync(teamProject, buildId).Wait();
            } catch (AggregateException ex) {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}