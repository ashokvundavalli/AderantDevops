using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.Tasks.WarningProcess;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Aderant.Build.Tasks {
    public sealed class WarningReporter {
        private readonly VssConnection vssConnection;
        private readonly WarningRatchetRequest request;
        private WarningComparison comparison;
        private BuildHttpClient client;

        public WarningReporter(VssConnection vssConnection, WarningRatchetRequest request) {
            if (vssConnection == null)
                throw new ArgumentNullException(nameof(vssConnection));

            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            this.vssConnection = vssConnection;
            this.client = vssConnection.GetClient<BuildHttpClient>(); // Return client is shared instance for all calls of GetClient, if you dispose it it's gone forever.
            this.request = request;
        }

        /// <summary>
        /// Creates the warning report. The analysis of what increased the warning count.
        /// </summary>
        /// <returns>System.String.</returns>
        public string CreateWarningReport() {
            return CreateWarningReportAsync().Result;
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
            Microsoft.TeamFoundation.Build.WebApi.Build thisBuild = request.Build;

            if (request.Build == null) {
                Microsoft.TeamFoundation.Build.WebApi.Build build = await client.GetBuildAsync(request.TeamProject, request.BuildId).ConfigureAwait(false);

                request.Build = thisBuild = build;
            }

            var lastBuild = await WarningRatchet.GetLastGoodBuildAsync(client, request);

            if (thisBuild != null && lastBuild != null) {
                request.LastGoodBuild = lastBuild;

                Stream first = await GetLogContentsAsync(client, request.TeamProject, lastBuild.Id).ConfigureAwait(false);
                if (first == null) {
                    throw new InvalidOperationException("Failed to get build log for build " + lastBuild.Id);
                }

                Stream second = await GetLogContentsAsync(client, request.TeamProject, thisBuild.Id).ConfigureAwait(false);
                if (second == null) {
                    throw new InvalidOperationException("Failed to get build log for build " + lastBuild.Id);
                }

                BuildLogProcessor processor = new BuildLogProcessor();
                comparison = processor.GetWarnings(first, second);

                return processor.CreateWarningReport(comparison, request.LastGoodBuild.Url);
            }

            return await Task.FromResult(string.Empty);
        }

        private async Task<Stream> GetLogContentsAsync(BuildHttpClient client, string requestTeamProject, int buildId) {
            var baseline = await client.GetBuildTimelineAsync(requestTeamProject, buildId).ConfigureAwait(false);

            return await GetLogAsync(client, requestTeamProject, buildId, baseline).ConfigureAwait(false);
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