using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
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

            logger.Info("Getting details for build: {0}", buildId);
            var buildDetail = await buildHttpClient.GetBuildAsync(buildId);

            logger.Info("Getting build work items");
            var workItemReferences = await buildHttpClient.GetBuildWorkItemsRefsAsync(teamProject, buildId);

            var workItems = await workItemClient.GetWorkItemsAsync(workItemReferences.Select(item => Int32.Parse(item.Id, CultureInfo.InvariantCulture)), null, null, WorkItemExpand.Relations);

            logger.Info("Processing work items");
            foreach (var workItem in workItems) {
                object workItemType;
                if (workItem.Fields.TryGetValue("System.WorkItemType", out workItemType)) {
                    string type = workItemType as string;

                    var patch = new JsonPatchDocument();

                    if (string.Equals("User Story", type, StringComparison.OrdinalIgnoreCase)) {
                        LinkToBuild(patch, buildDetail);
                    }

                    if (string.Equals("Task", type, StringComparison.OrdinalIgnoreCase)) {
                        LinkToBuild(patch, buildDetail);
                    }

                    if (string.Equals("Bug", type, StringComparison.OrdinalIgnoreCase)) {
                        AssociateBug(patch, buildDetail);
                    }

                    logger.Info(string.Format(CultureInfo.InvariantCulture, "Associating {0} to build {1}", workItem.Id, buildDetail.BuildNumber));
                    await workItemClient.UpdateWorkItemAsync(patch, workItem.Id.Value);
                } else {
                    logger.Info("Work item {0} does not have a System.WorkItemType field so cannot be associated to the build", workItem.Id);
                }
            }
        }

        private void LinkToBuild(JsonPatchDocument patch, Microsoft.TeamFoundation.Build.WebApi.Build buildDetail) {
            patch.Add(new JsonPatchOperation {
                Path = @"/relations/-",
                Operation = Operation.Add,
                Value = new WorkItemRelation {
                    Rel = "ArtifactLink",
                    Url = buildDetail.Uri.ToString(),
                    Attributes = CreateAttributes()
                }
            });
        }

        private IDictionary<string, object> CreateAttributes() {
            return new Dictionary<string, object> {
                {"name", "Build"},
                {"comment", "Integrated in build"}
            };
        }

        private void AssociateBug(JsonPatchDocument patch, Microsoft.TeamFoundation.Build.WebApi.Build buildDetail) {
            LinkToBuild(patch, buildDetail);

            patch.Add(new JsonPatchOperation {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Build.IntegrationBuild",
                Value = buildDetail.BuildNumber
            });
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