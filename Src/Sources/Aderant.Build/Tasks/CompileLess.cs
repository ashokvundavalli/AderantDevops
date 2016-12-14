using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace Aderant.Build.Tasks {
    public class CompileLess : Microsoft.Build.Utilities.Task {

        [Required]
        public ITaskItem[] LessFiles { get; set; }

        public override bool Execute() {
            var tasks = new List<Task>();
            Stopwatch sw = Stopwatch.StartNew();
            string pathToLessCompiler = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\LessCompiler\lessc.cmd";

            foreach (ITaskItem lessTaskItem in LessFiles) {
                Log.LogMessage($"Compiling LESS into CSS for file: {lessTaskItem.ItemSpec}");
                string cssOutputPath = $"{lessTaskItem.GetMetadata("RelativeDir")}{lessTaskItem.GetMetadata("FileName")}.css";
                tasks.Add(BuildInfrastructureHelper.StartProcessAsync(
                        "cmd.exe",
                        $"/c {pathToLessCompiler} -ru {lessTaskItem.ItemSpec} {cssOutputPath}",
                        ".",
                        OnReceiveStandardErrorOrOutputData)
                    );
            }

            Task.WaitAll(tasks.ToArray());

            sw.Stop();
            Log.LogMessage("CompileLess completed in " + sw.Elapsed.ToString("mm\\:ss\\.ff"), null);
            return !Log.HasLoggedErrors;
        }

        private void OnReceiveStandardErrorOrOutputData(DataReceivedEventArgs e, bool isError, System.Diagnostics.Process process) {
            if (e.Data != null) {
                if (isError) {
                    Log.LogError("{0}: {1}", process.Id.ToString(), e.Data);
                    Log.LogError("If you didn't intend for this LESS file to be compiled change the filename to begin with an underscore e.g. _Aderant.DontCompileMePls.less");
                } else {
                    Log.LogMessage("{0}: {1}", process.Id.ToString(), e.Data);
                }
            }
        }

    }
}