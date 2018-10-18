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

        public string OnErrorReason { get; set; }

        public string[] TaskObjects { get; set; }

        public bool LogScript { get; set; } = true;

        [Output]
        public string Result { get; set; }

        public override bool Execute() {
            try {
                BuildEngine3.Yield();

                string thisTaskExecutingDirectory = Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode);

                Dictionary<string, object> variables = new Dictionary<string, object>();
                if (TaskObjects != null) {
                    foreach (var key in TaskObjects) {
                        object registeredTaskObject = BuildEngine4.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build);

                        if (registeredTaskObject != null) {
                            Log.LogMessage(MessageImportance.Normal, "Extracted registered task object: " + key);
                        }

                        variables[key] = registeredTaskObject;
                    }
                }

                if (LogScript) {
                    Log.LogMessage(MessageImportance.Normal, "Executing script:\r\n{0}", ScriptBlock);
                }

                if (RunScript(variables, Log, thisTaskExecutingDirectory)) {
                    Log.LogError("Execution of script: '{0}' failed.", ScriptBlock);

                    using (var proxy = GetProxy()) {
                        proxy.SetStatus("Failed", OnErrorReason);
                    }
                }

                return !Log.HasLoggedErrors;
            } finally {
                BuildEngine3.Reacquire();
            }
        }

        public void Cancel() {
            cts.Cancel();
        }

        private bool RunScript(Dictionary<string, object> variables, TaskLoggingHelper name, string directoryName) {
            var pipelineExecutor = new PowerShellPipelineExecutor();
            pipelineExecutor.ProgressPreference = ProgressPreference;

            AttachLogger(name, pipelineExecutor);

            cts = new CancellationTokenSource();

            try {
                var scripts = new List<string>();

                string combine = Path.Combine(directoryName, "Build.psm1");
                if (File.Exists(combine)) {
                    scripts.Add(
                        $"Import-Module \"{directoryName}\\Build.psm1\""
                    );
                }

                scripts.Add(ScriptBlock);

                pipelineExecutor.RunScript(
                    scripts,
                    variables,
                    cts.Token);

                Result = pipelineExecutor.Result;

            } catch (OperationCanceledException) {
                // Cancellation was requested
            }

            return pipelineExecutor.ExecutionError;
        }

        private static void AttachLogger(TaskLoggingHelper log, PowerShellPipelineExecutor pipelineExecutor) {
            pipelineExecutor.DataReady += (sender, objects) => {
                foreach (var o in objects) {
                    log.LogMessage(MessageImportance.Normal, o.ToString());
                }
            };

            pipelineExecutor.ErrorReady += (sender, objects) => {
                foreach (var o in objects) {
                    log.LogError(o.ToString());
                }
            };

            pipelineExecutor.Debug += (sender, message) => { log.LogMessage(MessageImportance.Low, message.ToString()); };
            pipelineExecutor.Verbose += (sender, message) => { log.LogMessage(MessageImportance.Low, message.ToString()); };
            pipelineExecutor.Warning += (sender, message) => { log.LogWarning(message.ToString()); };
            pipelineExecutor.Info += (sender, message) => { log.LogMessage(message.ToString()); };
        }

        private IBuildPipelineService GetProxy() {
            return BuildPipelineServiceClient.Current;
        }
    }
}
