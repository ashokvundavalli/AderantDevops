using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.WebHooks;
using Microsoft.AspNet.WebHooks.Payloads;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aderant.TeamFoundation.Integration {
    public class IncomingWebHookDispatcher : VstsWebHookHandlerBase, IWebHookHandler {

        public override Task ExecuteAsync(WebHookHandlerContext context, BuildCompletedPayload payload) {
            return Task.Run(
                () => {
                    // Ignore TFVC builds - the definition type is always set to XAML even when it is not actually
                    // a XAML build.
                    if (payload.Resource.SourceGetVersion != null && payload.Resource.SourceGetVersion.StartsWith("C", StringComparison.OrdinalIgnoreCase)) {
                        return base.ExecuteAsync(context, payload);
                    }

                    try {
                        string artifactUrl = payload.Resource.Url + "/artifacts";

                        var notFoundRetryCount = 0;
                        var maxRetries = 5;
                        var delay = RetryDelay();
                        var rawArtifactJson = RetryOrDefault(
                            5,
                            delay,
                            () => GetArtifactsFromBuild(artifactUrl, maxRetries, ref notFoundRetryCount));

                        if (!string.IsNullOrWhiteSpace(rawArtifactJson)) {
                            PostMessageToAzureQueue(payload, rawArtifactJson);
                        }
                    } catch (Exception ex) {
                        Log(ex.Message);
                    }

                    return base.ExecuteAsync(context, payload);
                });
        }

        private static void Log(string message, EventLogEntryType type = EventLogEntryType.Error) {
            using (EventLog eventLog = new EventLog("Application")) {
                eventLog.Source = "TFS Web Hook Receiver";
                eventLog.WriteEntry(message, type);
            }
        }

        private static TimeSpan RetryDelay() {
            Random rnd = new Random();
            int number = rnd.Next(500, 3000);
            return TimeSpan.FromMilliseconds(number);
        }

        private string GetArtifactsFromBuild(string artifactUrl, int maxRetries, ref int notFoundRetryCount) {
            try {
                using (WebClient client = new WebClient()) {
                    client.UseDefaultCredentials = true;
                    return client.DownloadString(artifactUrl);
                }
            } catch (WebException wex) {
                notFoundRetryCount++;

                if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.NotFound) {
                    if (notFoundRetryCount > maxRetries) {
                        throw;
                    }
                }

                throw;
            }
        }

        private static T RetryOrDefault<T>(int times, TimeSpan delay, Func<T> operation) {
            var attempts = 0;
            do {
                try {
                    attempts++;
                    return operation();
                } catch (Exception) {
                    if (attempts == times) {
                        return default(T);
                    }

                    Task.Delay(delay).Wait();
                }
            } while (true);
        }

        private void PostMessageToAzureQueue(BuildCompletedPayload payload, string rawArtifactJson) {
            var artifactJson = JObject.Parse(rawArtifactJson);
            var artifactNode = artifactJson["value"];

            if (artifactNode != null && artifactNode.HasValues) {
                var buildEvent = new {
                    BuildId = payload.Resource.Id,
                    Url = payload.Resource.Url,
                };

                var root = JObject.FromObject(buildEvent);
                root.Add("Artifacts", artifactNode);

                var compressedData = GetMessageBytes(root);

                var cloudQueue = StorageFactory.GetCloudQueue();
                var message = new CloudQueueMessage(compressedData);

                // Messages cannot be larger than 65536 bytes.
                cloudQueue.AddMessage(message);
            }
        }

        private static byte[] GetMessageBytes(JObject root) {
            var eventJson = JsonConvert.SerializeObject(root);
            var data = Encoding.UTF8.GetBytes(eventJson);

            using (var compressedData = new MemoryStream()) {
                using (var zipStream = new GZipStream(compressedData, CompressionMode.Compress)) {
                    zipStream.Write(data, 0, data.Length);
                }

                return compressedData.ToArray();
            }
        }
    }

}