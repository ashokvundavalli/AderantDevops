using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.Tasks.WarningProcess;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Client;

namespace Aderant.Build.Tasks {
    public sealed class WarningReporter {
        private readonly VssConnection connection;
        private readonly string teamProject;
        private readonly int buildId;
        private WarningComparison comparison;

        public WarningReporter(VssConnection connection, string teamProject, int buildId) {
            this.connection = connection;
            this.teamProject = teamProject;
            this.buildId = buildId;
        }

        public Microsoft.TeamFoundation.Build.WebApi.Build LastGoodBuild { get; set; }

        /// <summary>
        /// Creates the warning report. The analysis of what increased the warning count.
        /// </summary>
        /// <returns>System.String.</returns>
        public string CreateWarningReport() {
            var task = CreateWarningReportAsync();
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Gets the adjusted warning count. This excludes transient warnings such as file copy problems.
        /// </summary>
        /// <returns>System.Int32.</returns>
        public int GetAdjustedWarningCount() {
            if (comparison != null) {
                return comparison.GetAdjustedCount();
            }
            throw new InvalidOperationException("This method cannot be called without first creating a report.");
        }
        
        private async Task<string> CreateWarningReportAsync() {
            if (LastGoodBuild == null) {
                BuildHttpClient client = connection.GetClient<BuildHttpClient>();
                Microsoft.TeamFoundation.Build.WebApi.Build buildDetails = await client.GetBuildAsync(teamProject, buildId);

                await WarningRatchet.GetLastGoodBuildAsync(teamProject, buildDetails.Definition.Id, client);
            }

            if (LastGoodBuild != null) {
                BuildHttpClient client = connection.GetClient<BuildHttpClient>();
                Stream first = await GetLogContentsAsync(client);
                Stream second = await GetLogContentsAsync(client);

                BuildLogProcessor processor = new BuildLogProcessor();
                comparison = processor.GetWarnings(first, second);

                return processor.CreateWarningReport(comparison, LastGoodBuild.Url);
            }

            return await Task.FromResult(string.Empty);
        }

        private async Task<Stream> GetLogContentsAsync(BuildHttpClient client) {
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