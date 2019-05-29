using System;
using System.Collections.Concurrent;
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
        private static ConcurrentDictionary<string, string[]> cache = new ConcurrentDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource cts;

        public bool UseResultCache { get; set; }

        public string ScriptBlock { get; set; }

        public string ScriptFromResource { get; set; }

        public string ProgressPreference { get; set; }

        public string OnErrorReason { get; set; }

        public string[] TaskObjects { get; set; }

        public bool LogScript { get; set; } = true;

        [Output]
        public string[] Result { get; set; }

        public override bool Execute() {
            Script script = null;

            if (!string.IsNullOrEmpty(ScriptBlock)) {
                script = new Script(ScriptBlock, ScriptBlock);
            } if (!string.IsNullOrEmpty(ScriptFromResource)) {
                script = new Script(LoadResource(ScriptFromResource), ScriptFromResource);

                this.ScriptBlock = script.Value;
            }

            if (script == null || script.Value == null) {
                ErrorUtilities.VerifyThrowArgument(ScriptFromResource != null, $"Neither {nameof(ScriptBlock)} or {nameof(ScriptFromResource)} is specified", null);
            }

            if (UseResultCache) {
                string[] result;
                if (cache.TryGetValue(script.CacheKey, out result)) {
                    Result = result;
                    return true;
                }
            }

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
                    Log.LogMessage(MessageImportance.Normal, "Executing script:\r\n{0}", script.Value);
                }

                try {
                    if (RunScript(variables, script.Value, Log, thisTaskExecutingDirectory)) {
                        FailTask(null, script.Value);
                    }
                } catch (Exception ex) {
                    FailTask(ex, script.Value);
                }

                if (UseResultCache) {
                    cache.TryAdd(script.CacheKey, Result);
                }

                return !Log.HasLoggedErrors;
            } finally {
                BuildEngine3.Reacquire();
            }
        }

        public void Cancel() {
            cts.Cancel();
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

        private void FailTask(Exception exception, string scriptValue) {
            Log.LogError("[Error] Execution of script: '{0}' failed.", scriptValue);

            if (exception != null) {
                Log.LogErrorFromException(exception);
            }

            using (var proxy = GetProxy()) {
                if (proxy != null) {
                    proxy.SetStatus("Failed", OnErrorReason);
                }
            }
        }

        private bool RunScript(Dictionary<string, object> variables, string scriptValue, TaskLoggingHelper name, string directoryName) {
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

                scripts.Add(scriptValue);

                pipelineExecutor.RunScript(
                    scripts,
                    variables,
                    cts.Token);

                if (pipelineExecutor.Result != null) {
                    Result = pipelineExecutor.Result.ToArray();
                }

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

        internal class Script {
            public Script(string value, string cacheKey) {
                Value = value;
                CacheKey = cacheKey;
            }

            public string CacheKey;

            public string Value;
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