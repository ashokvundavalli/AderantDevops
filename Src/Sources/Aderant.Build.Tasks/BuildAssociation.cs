using System;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Aderant.Build.Tasks {
    public class BuildAssociation {
        private VssConnection connection;

        public BuildAssociation(VssConnection connection) {
            this.connection = connection;
        }

        public async Task AssociateWorkItemsToBuildAsync(string teamProject, int buildId) {
            var buildHttpClient = connection.GetClient<BuildHttpClient>();
            var workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();

            var buildDetail = await buildHttpClient.GetBuildAsync(buildId);

            var workItemReferences = await buildHttpClient.GetBuildWorkItemsRefsAsync(teamProject, buildId);

            foreach (var result in workItemReferences) {
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
    }
}