using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// This module is for compiling LESS files.
    /// </summary>
    public class CompileLess : Microsoft.Build.Utilities.Task {

        /// <summary>
        /// Entrance from ≤Target Name="CompileLess"... ≤LessFiles
        /// </summary>
        [Required]
        public ITaskItem[] LessFiles { get; set; }

        /// <summary>
        /// This method contains debugging codes to capture more information for a random error on LESS compiling.
        /// </summary>
        /// <returns></returns>
        public override bool Execute() {
            var tasks = new List<Task>();
            Stopwatch sw = Stopwatch.StartNew();
            string pathToLessCompiler = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\LessCompiler\lessc.cmd";

            Log.LogMessage("LESS files to be processed:" + string.Join("," + Environment.NewLine, LessFiles.ToList()) + ".");

            foreach (ITaskItem lessTaskItem in LessFiles) {
                Log.LogMessage($"Compiling LESS into CSS for file: {lessTaskItem.ItemSpec}");
                string cssOutputPath = $"{lessTaskItem.GetMetadata("RelativeDir")}{lessTaskItem.GetMetadata("FileName")}.css";
                Log.LogMessage($"The command to run is: cmd.exe /c {pathToLessCompiler} -ru {lessTaskItem.ItemSpec} {cssOutputPath}. Hope it goes through.");
                tasks.Add(BuildInfrastructureHelper.StartProcessAsync(
                        "cmd.exe",
                        $"/c {pathToLessCompiler} -ru {lessTaskItem.ItemSpec} {cssOutputPath}",
                        ".",
                        OnReceiveStandardErrorOrOutputData)
                    );
            }

            try {
                Task.WaitAll(tasks.ToArray());
            } catch (Exception ex) {
                Log.LogMessage($"Error occured compiling LESS files: {ex.Message}");
                throw;
            }

            
            sw.Stop();
            Log.LogMessage("CompileLess completed in " + sw.Elapsed.ToString("mm\\:ss\\.ff"), null);
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Being triggered on StandOut or StandError.
        /// </summary>
        private void OnReceiveStandardErrorOrOutputData(DataReceivedEventArgs e, bool isError, System.Diagnostics.Process process) {
            if (e.Data != null) {
                if (isError) {
                    Log.LogError("{0}: e.Data={1}, Arguments={2}", process.Id.ToString(), e.Data, process.StartInfo.Arguments);
                    Log.LogError("If you didn't intend for this LESS file to be compiled change the filename to begin with an underscore e.g. _Aderant.DontCompileMePls.less");
                    string stderrx = process.StandardError.ReadToEnd();
                    Log.LogError($"StandardError={stderrx}. End of error printing.");
                } else {
                    Log.LogMessage("{0}: {1}", process.Id.ToString(), e.Data);
                }
            }
        }

    }
}