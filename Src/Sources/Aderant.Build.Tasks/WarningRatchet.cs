using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Aderant.Build.Tasks {
    public class WarningRatchet {
        private readonly VssConnection connection;
        private BuildHttpClient client;

        const int MaximumItemCount = 5000;

        public WarningRatchet(VssConnection connection) {
            this.connection = connection;
            this.client = connection.GetClient<BuildHttpClient>(); // Return client is shared instance for all calls of GetClient, if you dispose it it's gone forever.
        }

        public static async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetLastGoodBuildAsync(BuildHttpClient client, WarningRatchetRequest request) {
            //if (request.IsDraft && !string.IsNullOrEmpty(request.BuildDefinitionName)) {
            //    var references = await client.GetDefinitionsAsync(request.TeamProject, request.BuildDefinitionName, );
            //    if (references != null) {
            //        DefinitionReference reference = references.FirstOrDefault(item => item.Id != request.BuildDefinitionId);
            //        if (reference != null)
            //            request.BuildDefinitionId = reference.Id;
            //    }
            //}

            if (request.BuildDefinitionId == 0) {
                throw new InvalidOperationException("Cannot request a build with a definition id of 0.");
            }

            // This API must match the version that is deployed to the build agent or MissingMethodExceptions will occur as
            // the agent will have provided us with an incompatible API.
            // To fix this we need to run this code in a separate process/appdomain/container to that which is provided
            // by the build agent
            var result = await client.GetBuildsAsync(request.TeamProject, new int[] { request.BuildDefinitionId },
                queues: null,
                buildNumber: null,
                minFinishTime: null,
                maxFinishTime: null,
                requestedFor: null,
                reasonFilter: BuildReason.All,
                statusFilter: BuildStatus.Completed,
                resultFilter: BuildResult.PartiallySucceeded | BuildResult.Succeeded,
                tagFilters: null,
                properties: null,
                top: 1,
                continuationToken: null,
                maxBuildsPerDefinition: MaximumItemCount,
                deletedFilter: QueryDeletedOption.ExcludeDeleted,
                queryOrder: BuildQueryOrder.FinishTimeDescending,
                branchName: request.DestinationBranchName,
                userState: null);

            Microsoft.TeamFoundation.Build.WebApi.Build build = result.FirstOrDefault();
            return build;
        }

        /// <summary>
        /// Gets the last good build warning count.
        /// </summary>
        /// <param name="request">The request.</param>
        public async Task<int?> GetLastGoodBuildWarningCountAsync(WarningRatchetRequest request) {
            var build = await GetLastGoodBuildAsync(client, request).ConfigureAwait(false);

            if (build != null) {
                request.LastGoodBuild = build;
                var timelineRecords = await client.GetBuildTimelineAsync(request.TeamProject, build.Id).ConfigureAwait(false);
                return SumWarnings(timelineRecords);
            }

            return null;
        }

        private static int SumWarnings(Timeline timelineRecords) {
            return timelineRecords.Records
                .Where(s => s.WarningCount != null)
                .Sum(s => s.WarningCount)
                .GetValueOrDefault();
        }

        /// <summary>
        /// Gets the last good build warning count.
        /// </summary>
        /// <param name="request">The request.</param>
        public int? GetLastGoodBuildWarningCount(WarningRatchetRequest request) {
            var task = GetLastGoodBuildWarningCountAsync(request);

            try {
                return task.Result;
            } catch (AggregateException ex) {
                if (ex.InnerException != null) {
                    throw ex.InnerException;
                }
                throw;
            }
        }

        public async Task<int> GetBuildWarningCountAsync(WarningRatchetRequest request) {
            if (request.Build == null) {
                var build = await client.GetBuildAsync(request.TeamProject, request.BuildId);

                request.Build = build;
            }

            var timelineRecords = await client.GetBuildTimelineAsync(request.TeamProject, request.Build.Id);

            return SumWarnings(timelineRecords);
        }

        public int GetBuildWarningCount(WarningRatchetRequest request) {
            var task = GetBuildWarningCountAsync(request);

            try {
                return task.Result;
            } catch (AggregateException ex) {
                if (ex.InnerException != null) {
                    throw ex.InnerException;
                }
                throw;
            }
        }

        public WarningReporter GetWarningReporter(WarningRatchetRequest request) {
            var reporter = new WarningReporter(connection, request);

            return reporter;
        }

        public WarningRatchetRequest CreateNewRequest(string teamProject, int buildId, string destinationBranchName) {
            var build = client.GetBuildAsync(teamProject, buildId).Result;

            return new WarningRatchetRequest {
                TeamProject = teamProject,
                BuildId = buildId,
                Build = build,
                BuildDefinitionId = build.Definition.Id,
                DestinationBranchName = destinationBranchName,
            };
        }
    }
}