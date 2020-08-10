using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using Aderant.Build.PipelineService;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.PowerShell {

    /// <summary>
    /// Executes PowerShell as part of the build. This could also be implemented as a TaskFactory for typed property support.
    /// </summary>
    public class PowerShellScript : Task, ICancelableTask {
        private CancellationTokenSource cts;

        /// <summary>
        /// The ScriptBlock to execute. Any valid PowerShell is accepted.
        /// </summary>
        public string ScriptBlock { get; set; }

        /// <summary>
        /// The path to a PowerShell script file.
        /// </summary>
        public string ScriptFile { get; set; }

        public string ProgressPreference { get; set; }

        public string OnErrorReason { get; set; }

        public string[] TaskObjects { get; set; }

        public bool LogScript { get; set; } = true;

        /// <summary>
        /// An array of arguments that will be provided to the <see cref="ScriptBlock"/> or <see cref="ScriptFile"/>.
        /// Arguments will be passed by splatting
        /// Using named parameters is recommended.
        /// </summary>
        public ITaskItem[] ScriptArguments { get; set; }

        [Output]
        public string[] Result { get; set; }

        public override bool Execute() {
            Script script = null;

            if (!string.IsNullOrEmpty(ScriptBlock)) {
                script = new Script(ScriptBlock, false);
            } else if (!string.IsNullOrEmpty(ScriptFile)) {
                script = new Script(ScriptFile, true);
            }

            ErrorUtilities.VerifyThrowArgument(script != null, $"{nameof(ScriptBlock)} is not specified", null);

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

                script.Arguments = ConvertArguments(ScriptArguments);

                if (LogScript) {
                    Log.LogMessage(MessageImportance.Normal, "Executing script:{0}{1}", Environment.NewLine, script.Value);
                }

                try {
                    if (RunScript(variables, script, Log, thisTaskExecutingDirectory)) {
                        FailTask(null, script);
                    }
                } catch (Exception ex) {
                    FailTask(ex, script);
                }

                return !Log.HasLoggedErrors;
            } finally {
                BuildEngine3.Reacquire();
            }
        }

        private CommandParameterCollection ConvertArguments(ITaskItem[] scriptArguments) {
            if (scriptArguments == null) {
                return null;
            }

            var collection = new CommandParameterCollection();

            foreach (ITaskItem item in scriptArguments) {
                var customNames = (IDictionary<string, string>)item.CloneCustomMetadata();

                foreach (var arg in customNames) {
                    collection.Add(new CommandParameter(arg.Key, arg.Value));
                }
            }

            return collection;
        }

        public void Cancel() {
            cts.Cancel();
        }

        private void FailTask(Exception exception, Script script) {
            Log.LogError("[Error] Execution of script: {0} failed.", script.Value.Quote());

            if (exception != null) {
                Log.LogErrorFromException(exception);
            }

            try {
                using (var proxy = GetProxy()) {
                    if (proxy != null) {
                        proxy.SetStatus("Failed", OnErrorReason);
                    }
                }
            } catch {
                // If we cannot connect to the build service then we don't mind as
            }
        }

        private bool RunScript(Dictionary<string, object> variables, Script script, TaskLoggingHelper name, string directoryName) {
            cts = new CancellationTokenSource();

            var processRunner = new ProcessRunner(new Exec {BuildEngine = this.BuildEngine}, cts.Token);

            var pipelineExecutor = new PowerShellPipelineExecutor {
                ProcessRunner = processRunner
            };

            pipelineExecutor.ProgressPreference = ProgressPreference;

            AttachLogger(name, pipelineExecutor);

            try {
                var command = new PSCommand();

                if (directoryName != null) {
                    string buildPowerShellModule = Path.Combine(directoryName, "Build.psm1");
                    if (File.Exists(buildPowerShellModule)) {
                        command.AddScript($"Import-Module '{buildPowerShellModule}' -DisableNameChecking");
                        command.AddStatement();
                    }
                }

                if (script.IsFile) {
                    command.AddCommand(script.Value);
                    if (script.Arguments != null) {
                        foreach (var arg in script.Arguments) {
                            command.AddParameter(arg.Name, GetRealArgValue(arg));
                        }
                    }
                } else {
                    StringBuilder builder = new StringBuilder();

                    builder.AppendLine("process {");
                    builder.AppendLine("$namedParameters = $args[1]");
                    builder.AppendLine("$namedParameters | Format-Table | Out-String");
                    builder.AppendLine(". $args[0] @namedParameters");
                    builder.AppendLine("}");

                    var parameterTable = BuildParameterTable(script);

                    command.AddScript(builder.ToString());
                    command.AddArgument(System.Management.Automation.ScriptBlock.Create(script.Value));
                    command.AddArgument(parameterTable);
                }

                command.AddStatement();

                pipelineExecutor.RunScript(
                    command,
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

        private static Hashtable BuildParameterTable(Script script) {
            Hashtable parameterTable = new Hashtable();
            if (script.Arguments != null) {
                foreach (var arg in script.Arguments) {
                    parameterTable.Add(arg.Name, GetRealArgValue(arg));
                }
            }

            return parameterTable;
        }

        private static object GetRealArgValue(CommandParameter arg) {
            var value = arg.Value;

            string stringValue = value.ToString();
            if (string.Equals("$true", stringValue, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (string.Equals("$false", stringValue, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (stringValue.Contains(";")) {
                var p = new Project();
                return p.AddItem("_", stringValue).Select(s => s.EvaluatedInclude).ToArray();
            }

            return value;
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

            public Script(string value, bool isFile) {
                IsFile = isFile;
                Value = value;
            }

            public string Value;

            public CommandParameterCollection Arguments { get; set; }

            public bool IsFile { get; }
        }
    }

    /// <summary>
    /// Wraps the build engine command runner
    /// </summary>
    internal class ProcessRunner {
        private static bool taskCancelled;

        public ProcessRunner(Exec execTask, CancellationToken ctsToken) {
            using (var registration = ctsToken.Register(() => TryCancelTask(execTask))) {
                StartProcess = command => {
                    var cancelEventHandler = new ConsoleCancelEventHandler((sender, args) => { TryCancelTask(execTask); });

                    try {
                        Console.CancelKeyPress += cancelEventHandler;

                        execTask.Command = command.FileName + " " + command.Arguments;
                        execTask.IgnoreExitCode = false;
                        execTask.WorkingDirectory = command.WorkingDirectory;
                        execTask.IgnoreStandardErrorWarningFormat = true;
                        execTask.IgnoreStandardErrorWarningFormat = true;

                        execTask.StdErrEncoding = execTask.StdOutEncoding = Encoding.UTF8.BodyName;

                        if (command.Environment != null) {
                            execTask.EnvironmentVariables = command.Environment.Select(s => s.Key + "=" + s.Value).ToArray();
                        }

                        execTask.Timeout = (int) TimeSpan.FromMinutes(25).TotalMilliseconds;
                        execTask.Execute();
                        return execTask.ExitCode;
                    } finally {
                        Console.CancelKeyPress -= cancelEventHandler;
                        registration.Dispose();
                    }
                };
            }
        }

        private static void TryCancelTask(Exec execTask) {
            if (taskCancelled) {
                return;
            }

            taskCancelled = true;
            execTask.Cancel();
        }

        public Func<ProcessStartInfo, int> StartProcess { get; set; }
    }
}