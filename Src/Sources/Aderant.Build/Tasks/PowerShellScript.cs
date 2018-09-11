using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Aderant.Build.PipelineService;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class PowerShellScript : Task, ICancelableTask {
        private CancellationTokenSource cts;

        [Required]
        public string ScriptBlock { get; private set; }

        public string ProgressPreference { get; set; }

        public bool MeasureCommand { get; set; }

        public bool ProvideBuildContext { get; set; }

        [Output]
        public string Result { get; set; }

        public override bool Execute() {
            try {
                BuildEngine3.Yield();

                string directoryName = Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode);

                Log.LogMessage(MessageImportance.Normal, "Executing script:\r\n{0}", ScriptBlock);

                RunScript(directoryName);

                return !Log.HasLoggedErrors;
            } finally {
                BuildEngine3.Reacquire();
            }
        }

        public void Cancel() {
            cts.Cancel();
        }

        private void RunScript(string directoryName) {
            var pipelineExecutor = new PowerShellPipelineExecutor();
            pipelineExecutor.ProgressPreference = ProgressPreference;
            pipelineExecutor.MeasureCommand = MeasureCommand;

            pipelineExecutor.Debug += (sender, message) => { Log.LogMessage(MessageImportance.Normal, message); };
            pipelineExecutor.Verbose += (sender, message) => { Log.LogMessage(MessageImportance.Normal, message); };
            pipelineExecutor.Warning += (sender, message) => { Log.LogWarning(message); };
            pipelineExecutor.Error += (sender, message) => { Log.LogError(message); };

            pipelineExecutor.Output += (sender, message) => { Log.LogMessage(MessageImportance.Normal, message); };

            cts = new CancellationTokenSource();

            try {
                BuildOperationContext operationContext = null;

                Dictionary<string, object> variables = new Dictionary<string, object>();

                using (var contract = GetProxy()) {
                    if (contract != null) {
                        operationContext = contract.GetContext();
                        variables["TaskContext"] = operationContext;
                        variables["BuildLogFile"] = operationContext.LogFile;
                    }

                    pipelineExecutor.RunScript(
                        new[] {
                            $"Import-Module {directoryName}\\Build.psm1",
                            ScriptBlock
                        },
                        variables,
                        cts.Token).Wait(cts.Token);

                    Result = pipelineExecutor.Result;

                    if (contract != null) {
                        contract.Publish(operationContext);
                    }
                }
            } catch (OperationCanceledException) {
                // Cancellation was requested
            }
        }

        private IBuildPipelineService GetProxy() {
            if (ProvideBuildContext) {
                return BuildPipelineServiceProxy.Current;
            }

            return null;
        }
    }
}
