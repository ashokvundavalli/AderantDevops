using System;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class PowerShellScript : Task, ICancelableTask {
        private CancellationTokenSource cts;

        [Required]
        public string ScriptBlock { get; private set; }

        public string ProgressPreference { get; set; }

        public bool MeasureCommand { get; set; }

        [Output]
        public string Result { get; set; }

        public override bool Execute() {
            Log.LogMessage(MessageImportance.Normal, "Executing script:\r\n{0}", ScriptBlock);

            var pipelineExecutor = new PowerShellPipelineExecutor();
            pipelineExecutor.ProgressPreference = ProgressPreference;
            pipelineExecutor.MeasureCommand = MeasureCommand;

            pipelineExecutor.Debug += (sender, message) => { Log.LogMessage(MessageImportance.Normal, message); };
            pipelineExecutor.Verbose += (sender, message) => { Log.LogMessage(MessageImportance.Normal, message); };
            pipelineExecutor.Warning += (sender, message) => { Log.LogWarning(message); };
            pipelineExecutor.Error += (sender, message) => { Log.LogError(message); };

            pipelineExecutor.Output += (sender, message) => { Log.LogMessage(MessageImportance.Normal, message); };

            this.cts = new CancellationTokenSource();

            try {
                pipelineExecutor.RunScript(ScriptBlock, cts.Token).Wait(cts.Token);
                Result = pipelineExecutor.Result;
            } catch (OperationCanceledException) {
                // Cancellation was requested
            }

            return !Log.HasLoggedErrors;
        }

        public void Cancel() {
            cts.Cancel();
        }
    }
}
