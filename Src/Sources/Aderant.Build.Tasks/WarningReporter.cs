using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.Tasks.WarningProcess;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Client;

namespace Aderant.Build.Tasks {
    public sealed class WarningReporter {
        private readonly VssConnection vssConnection;
        private readonly WarningRatchetRequest request;
        private WarningComparison comparison;

        public WarningReporter(VssConnection vssConnection, WarningRatchetRequest request) {
            this.vssConnection = vssConnection;
            this.request = request;
        }

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
            if (request.Build == null) {
                BuildHttpClient client = vssConnection.GetClient<BuildHttpClient>();
                var build = await client.GetBuildAsync(request.TeamProject, request.BuildId);

                request.Build = build;

                await WarningRatchet.GetLastGoodBuildAsync(client, request);
            }

            if (request.Build != null) {
                BuildHttpClient client = vssConnection.GetClient<BuildHttpClient>();
                Stream first = await GetLogContentsAsync(client, request);
                Stream second = await GetLogContentsAsync(client, request);

                BuildLogProcessor processor = new BuildLogProcessor();
                comparison = processor.GetWarnings(first, second);

                return processor.CreateWarningReport(comparison, request.Build.Url);
            }

            return await Task.FromResult(string.Empty);
        }

        private async Task<Stream> GetLogContentsAsync(BuildHttpClient client, WarningRatchetRequest warningRatchetRequest) {
            var baseline = await client.GetBuildTimelineAsync(warningRatchetRequest.TeamProject, warningRatchetRequest.BuildId);

            return await GetLogAsync(client, warningRatchetRequest.TeamProject, warningRatchetRequest.BuildId, baseline);
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