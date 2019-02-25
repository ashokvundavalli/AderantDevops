using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Aderant.Build.PipelineService;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.PowerShell {
    public class PowerShellScript : Task, ICancelableTask {
        private CancellationTokenSource cts;
        private string scriptBlock;

        public string ScriptBlock {
            get {
                if (scriptBlock == null) {
                    if (ScriptResource == null) {
                        ErrorUtilities.VerifyThrowArgument(ScriptResource != null, $"Neither {nameof(ScriptBlock)} or {nameof(ScriptResource)} is specified", null);
                    }
                    ScriptBlock = LoadResource(ScriptResource);
                }
                return scriptBlock;
            }
            set { scriptBlock = value; }
        }

        private string LoadResource(string scriptResource) {
            var asm = Assembly.GetExecutingAssembly();
            string[] names = asm.GetManifestResourceNames();

            foreach (string name in names) {
                var resourceName = typeof(PowerShellScript).Namespace + ".Resources." + scriptResource + ".ps1";

                if (string.Equals(name, resourceName)) {
                    using (Stream manifestResourceStream = asm.GetManifestResourceStream(resourceName)) {
                        using (var reader = new StreamReader(manifestResourceStream)) {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }

            throw new ArgumentException("There is no resource: " + scriptResource);
        }

        public string ScriptResource { get; set; }

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

                try {
                    if (RunScript(variables, Log, thisTaskExecutingDirectory)) {
                        FailTask(null);
                    }
                } catch (Exception ex) {
                    FailTask(ex);
                }

                return !Log.HasLoggedErrors;
            } finally {
                BuildEngine3.Reacquire();
            }
        }

        public void Cancel() {
            cts.Cancel();
        }

        private void FailTask(Exception exception) {
            Log.LogError("[Error] Execution of script: '{0}' failed.", ScriptBlock);

            if (exception != null) {
                Log.LogErrorFromException(exception);
            }

            using (var proxy = GetProxy()) {
                if (proxy != null) {
                    proxy.SetStatus("Failed", OnErrorReason);
                }
            }
        }

        private bool RunScript(Dictionary<string, object> variables, TaskLoggingHelper name, string directoryName) {

            var processRunner = new ProcessRunner(new Exec { BuildEngine = this.BuildEngine });
            var pipelineExecutor = new PowerShellPipelineExecutor {
                ProcessRunner = processRunner
            };

            pipelineExecutor.ProgressPreference = ProgressPreference;

            AttachLogger(name, pipelineExecutor);

            cts = new CancellationTokenSource();

            try {
                var scripts = new List<string>();

                if (directoryName != null) {
                    string combine = Path.Combine(directoryName, "Build.psm1");
                    if (File.Exists(combine)) {
                        scripts.Add(
                            $"Import-Module \"{directoryName}\\Build.psm1\""
                        );
                    }
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

            return pipelineExecutor.HadErrors;
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

        internal virtual IBuildPipelineService GetProxy() {
            return BuildPipelineServiceClient.GetCurrentProxy();
        }
    }

    /// <summary>
    /// Wraps the build engine command runner
    /// </summary>
    internal class ProcessRunner {

        public ProcessRunner(Exec execTask) {

            StartProcess = command => {
                var cancelEventHandler = new ConsoleCancelEventHandler((sender, args) => { execTask.Cancel(); });

                try {
                    Console.CancelKeyPress += cancelEventHandler;

                    execTask.Command = command.FileName + " " + command.Arguments;
                    execTask.IgnoreExitCode = false;
                    execTask.WorkingDirectory = command.WorkingDirectory;
                    execTask.IgnoreStandardErrorWarningFormat = true;
                    execTask.IgnoreStandardErrorWarningFormat = true;

                    if (command.Environment != null) {
                        execTask.EnvironmentVariables = command.Environment.Select(s => s.Key + "=" + s.Value).ToArray();
                    }

                    execTask.Timeout = (int)TimeSpan.FromMinutes(20).TotalMilliseconds;
                    execTask.Execute();
                    return execTask.ExitCode;
                } finally {
                    Console.CancelKeyPress -= cancelEventHandler;
                }
            };
        }

        public Func<ProcessStartInfo, int> StartProcess { get; set; }
    }
}
