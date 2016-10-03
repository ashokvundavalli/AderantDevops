using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Client;

namespace Aderant.Build.Tasks {
    public sealed class WarningRatchetRequest {
        public string TeamProject { get; set; }
        public int BuildId { get; set; }
        public int BuildDefinitionId { get; set; }
        public string BuildDefinitionName { get; set; }
        public bool IsDraft { get; set; }
        public Microsoft.TeamFoundation.Build.WebApi.Build Build { get; internal set; }
    }

    public class WarningRatchet {
        private readonly VssConnection connection;

        const int MaximumItemCount = 5000;

        public WarningRatchet(VssConnection connection) {
            this.connection = connection;
        }

        public Microsoft.TeamFoundation.Build.WebApi.Build LastGoodBuild { get; private set; }

        private async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetLastGoodBuildAsync(WarningRatchetRequest request) {
            var client = connection.GetClient<BuildHttpClient>();

            var build = await GetLastGoodBuildAsync(client, request);
            if (build != null) {
                LastGoodBuild = build;

                return build;
            }

            return null;
        }

        public static async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetLastGoodBuildAsync(BuildHttpClient client, WarningRatchetRequest request) {
            string master = "refs/heads/master";

            if (request.IsDraft && !string.IsNullOrEmpty(request.BuildDefinitionName)) {
                List<DefinitionReference> references = await client.GetDefinitionsAsync(request.TeamProject, request.BuildDefinitionName, DefinitionType.Build);
                if (references != null) {
                    DefinitionReference reference = references.FirstOrDefault(item => item.Id != request.BuildDefinitionId);
                    if (reference != null)
                        request.BuildDefinitionId = reference.Id;
                }
            }

            if (request.BuildDefinitionId == 0) {
                throw new InvalidOperationException("Cannot request a builds with a definition of zero.");
            }

            var result = await client.GetBuildsAsync(request.TeamProject, new int[] { request.BuildDefinitionId },
                queues: null,
                buildNumber: null,
                minFinishTime: null,
                maxFinishTime: null,
                requestedFor: null,
                reasonFilter: BuildReason.All,
                statusFilter: BuildStatus.Completed,
                resultFilter: BuildResult.Succeeded,
                tagFilters: null,
                properties: null,
                type: DefinitionType.Build,
                top: 1,
                continuationToken: null,
                maxBuildsPerDefinition: MaximumItemCount,
                deletedFilter: QueryDeletedOption.ExcludeDeleted,
                queryOrder: BuildQueryOrder.FinishTimeDescending,
                branchName: master,
                userState: null);

            Microsoft.TeamFoundation.Build.WebApi.Build build = result.FirstOrDefault();
            return build;
        }

        /// <summary>
        /// Gets the last good build warning count.
        /// </summary>
        /// <param name="request">The request.</param>
        public async Task<int?> GetLastGoodBuildWarningCountAsync(WarningRatchetRequest request) {
            var build = await GetLastGoodBuildAsync(request);

            if (build != null) {
                request.Build = build;
                BuildHttpClient client = connection.GetClient<BuildHttpClient>();

                Timeline timelineRecords = await client.GetBuildTimelineAsync(request.TeamProject, build.Id);
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
            var client = connection.GetClient<BuildHttpClient>();
            var build = await client.GetBuildAsync(request.TeamProject, request.BuildId);

            request.Build = build;

            var timelineRecords = await client.GetBuildTimelineAsync(request.TeamProject, build.Id);

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
    }
}