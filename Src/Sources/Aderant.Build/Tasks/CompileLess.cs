using System.Diagnostics;
using System.IO;
using System.Threading;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aderant.Build.Tasks {
    public class CompileLess : Microsoft.Build.Utilities.Task{
        private Logging.ILogger logger;

        [Required]
        public string ModulesRootPath { get; set; }

        [Required]
        public string ModuleName { get; set; }
        
        public override bool Execute() {
            ModulesRootPath = Path.GetFullPath(ModulesRootPath);

            LogParameters();

            logger = new BuildTaskLogger(this);
            var tasks = new List<Task>();

            var pathToContent = $@"{ModulesRootPath}Src\{ModuleName}\Content";
            List<string> lessFilesToCompile = new List<string>();

            foreach (string file in Directory.GetFiles(pathToContent)) {
                if (file.Contains("Skin.") && file.EndsWith(".less")) {
                    Log.LogMessage($"Compiling less into css for file {file}");
                    string cssOutputFileName = file.Replace(".less", ".css");

                    tasks.Add(BuildInfrastructureHelper.StartProcessAsync(
                            "cmd.exe", 
                            $"/c lessc -ru {file} > {cssOutputFileName}", 
                            pathToContent, 
                            OnReceiveStandardErrorOrOutputData)
                        );
                }
            }

            Task.WaitAll(tasks.ToArray());
            return !Log.HasLoggedErrors;
        }

        private void OnReceiveStandardErrorOrOutputData(DataReceivedEventArgs e, bool isError, System.Diagnostics.Process process) {
            if (e.Data != null) {
                if (isError) {
                    logger.Error("{0}: {1}", process.Id.ToString(), e.Data);
                } else {
                    logger.Debug("{0}: {1}", process.Id.ToString(), e.Data);
                }
            }
        }

        private void LogParameters() {
            Log.LogMessage(MessageImportance.Normal, $"ModulesRootPath: {ModulesRootPath}" , null);
            Log.LogMessage(MessageImportance.Normal, $"ModuleName: {ModuleName}", null);
        }

    }
}