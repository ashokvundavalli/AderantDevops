using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Client;

namespace Aderant.Build.Tasks {
    public class WarningRatchet {
        private readonly VssConnection connection;

        const int MaximumItemCount = 5000;

        public WarningRatchet(VssConnection connection) {
            this.connection = connection;
        }

        public int LastGoodBuildId {
            get {
                if (LastGoodBuild != null) {
                    return LastGoodBuild.Id;
                }
                return 0;
            }
        }

        public Microsoft.TeamFoundation.Build.WebApi.Build LastGoodBuild { get; private set; }

        private async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetLastGoodBuildAsync(string teamProject, int buildDefinitionId) {
            var client = connection.GetClient<BuildHttpClient>();

            var build = await GetLastGoodBuildAsync(teamProject, buildDefinitionId, client);
            if (build != null) {
                LastGoodBuild = build;

                return build;
            }

            return null;
        }

        public static async Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetLastGoodBuildAsync(string teamProject, int buildDefinitionId, BuildHttpClient client) {
            string master = "refs/heads/master";

            var result = await client.GetBuildsAsync(teamProject, new int[] { buildDefinitionId },
                queues: null,
                buildNumber: null,
                minFinishTime: null,
                maxFinishTime: null,
                requestedFor: null,
                reasonFilter: BuildReason.All,
                statusFilter: BuildStatus.Completed,
                resultFilter: BuildResult.Succeeded,
                tagFilters: null, properties: null,
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

        public async Task<int?> GetLastGoodBuildWarningCountAsync(string teamProject, int buildDefinitionId) {
            var client = connection.GetClient<BuildHttpClient>();
            var build = await GetLastGoodBuildAsync(teamProject, buildDefinitionId);

            if (build != null) {
                var timelineRecords = await client.GetBuildTimelineAsync(teamProject, build.Id);
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

        public int? GetLastGoodBuildWarningCount(string teamProject, int buildDefinition) {
            var task = GetLastGoodBuildWarningCountAsync(teamProject, buildDefinition);

            try {
                return task.Result;
            } catch (AggregateException ex) {
                if (ex.InnerException != null) {
                    throw ex.InnerException;
                }
                throw;
            }
        }

        public async Task<int> GetBuildWarningCountAsync(string teamProject, int buildId) {
            var client = connection.GetClient<BuildHttpClient>();
            var build = await client.GetBuildAsync(teamProject, buildId);

            var timelineRecords = await client.GetBuildTimelineAsync(teamProject, build.Id);

            return SumWarnings(timelineRecords);
        }

        public int GetBuildWarningCount(string teamProject, int buildId) {
            var task = GetBuildWarningCountAsync(teamProject, buildId);
            task.Wait();
            return task.Result;
        }

        public WarningReporter GetWarningReporter(string teamProject, int buildId) {
            var reporter = new WarningReporter(connection, teamProject, buildId);

            

            reporter.LastGoodBuild = LastGoodBuild;
            return reporter;
        }
    }
}