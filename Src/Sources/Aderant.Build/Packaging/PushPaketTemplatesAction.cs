using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {

    public class PushPaketTemplatesAction {
        private ILogger logger;

        /// <summary>
        /// Executes this action.
        /// </summary>
        /// <param name="loggerInstance">The logger instance.</param>
        /// <param name="buildScriptsDirectory">The build scripts directory.</param>
        /// <param name="rootFolder">The root folder.</param>
        /// <param name="executeInParallel">if set to <c>true</c> this action is executed in parallel.</param>
        /// <returns>
        /// true, if executed successfully, otherwise false
        /// </returns>
        public bool Execute(ILogger loggerInstance, string buildScriptsDirectory, string rootFolder, bool executeInParallel = false) {
            this.logger = loggerInstance;
            var sw = new Stopwatch();
            sw.Start();

            this.logger.Info("Pushing paket templates to nuget server {0}", Constants.PackageServerUrlV3);
            try {
                var tasks = new List<Task>();

                // inspect third party folder
                foreach (var moduleFolder in Directory.EnumerateDirectories(rootFolder).Where(d => !d.EndsWith("Build.Infrastructure", StringComparison.InvariantCultureIgnoreCase))) {

                    var packageFile = Directory.GetFiles(moduleFolder).Single(f => f.EndsWith(".nupkg"));

                    this.logger.Info(string.Empty);
                    this.logger.Info("Pushing {0}...", moduleFolder.Split('\\').Last());

                    // push nuget package to the server
#if FEATURE_AZURE_NUGET
                    var arguments = string.Format(@"push url {0} file {1} apikey {2}", Constants.PackageServerUrlV3, packageFile, Constants.NugetServerApiKey);
#else
                    var arguments = string.Format(@"push url {0} file {1} apikey {2}", Constants.PackageServerUrl, packageFile, Constants.NugetServerApiKey);
#endif

                    var processFilePath = Path.Combine(buildScriptsDirectory, "paket.exe");
                    if (executeInParallel) {
                        tasks.Add(BuildInfrastructureHelper.StartProcessAsync(processFilePath, arguments, moduleFolder, OnReceiveStandardErrorOrOutputData));
                    } else {
                        BuildInfrastructureHelper.StartProcessAndWaitForExit(processFilePath, arguments, moduleFolder, OnReceiveStandardErrorOrOutputData);
                    }
                }

                Task.WaitAll(tasks.ToArray());

                sw.Stop();
                this.logger.Info(string.Empty);
                this.logger.Info("Finished paket pushing in {0} seconds.", (sw.ElapsedMilliseconds / 1000.0).ToString("F1"));
            } catch (Exception ex) {
                this.logger.Error("Error pushing paket templates to nuget server:");
                this.logger.Error(ex.ToString());
                return false;
            }
            return true;
        }

        private void OnReceiveStandardErrorOrOutputData(DataReceivedEventArgs e, bool isError, System.Diagnostics.Process process) {
            if (e.Data != null) {
                if (isError) {
                    this.logger.Error("{0}: {1}", process.Id.ToString(), e.Data);
                } else {
                    this.logger.Debug("{0}: {1}", process.Id.ToString(), e.Data);
                }
            }
        }
    }
}
