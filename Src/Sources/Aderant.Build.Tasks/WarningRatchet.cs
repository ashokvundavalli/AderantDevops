using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Aderant.Build.Tasks.WarningProcess;
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
            if (build != null) {
                LastGoodBuild = build;

                return build;
            }

            return null;
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
                task.Wait();
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

        public string CreateWarningReport(string teamProject, int buildId) {
            var task = CreateWarningReportAsync(teamProject, buildId);
            task.Wait();
            return task.Result;
        }

        private async Task<string> CreateWarningReportAsync(string teamProject, int buildId) {
            if (LastGoodBuild == null) {
                var client = connection.GetClient<BuildHttpClient>();
                var buildDetails = await client.GetBuildAsync(teamProject, buildId);
                await GetLastGoodBuildAsync(teamProject, buildDetails.Definition.Id);
            }

            if (LastGoodBuild != null) {
                BuildHttpClient client = connection.GetClient<BuildHttpClient>();
                Stream first = await GetLogContentsAsync(teamProject, LastGoodBuildId, client);
                Stream second = await GetLogContentsAsync(teamProject, buildId, client);

                BuildLogProcessor processor = new BuildLogProcessor();
                WarningComparison comparison = processor.GetWarnings(first, second);

                return processor.CreateWarningReport(comparison, LastGoodBuild.Url);
            }

            return await Task.FromResult(string.Empty);
        }

        private async Task<Stream> GetLogContentsAsync(string teamProject, int buildId, BuildHttpClient client) {
            var baseline = await client.GetBuildTimelineAsync(teamProject, buildId);

            return await GetLogAsync(client, teamProject, buildId, baseline);
        }

        private static Task<Stream> GetLogAsync(BuildHttpClient client, string teamProject, int buildId, Timeline timeline) {
            var logRecord = timeline.Records.FirstOrDefault(record => string.Equals(record.Name, "Run build pipeline", StringComparison.OrdinalIgnoreCase)
                                                                      && record.Log != null
                                                                      && record.Log.Id > 0);

            if (logRecord != null) {
                return client.GetBuildLogAsync(teamProject, buildId, logRecord.Log.Id);
            }

            return Task.FromResult(Stream.Null);
        }
    }
}