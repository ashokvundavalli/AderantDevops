using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Aderant.Build.Tasks.PowerShell;

namespace Aderant.Build {
    internal class PowerShellPipelineExecutor {

        public string ProgressPreference { get; set; }

        /// <summary>
        /// The scalar script result.
        /// </summary>
        public List<string> Result { get; set; }

        public bool HadErrors { get; private set; }
        public ProcessRunner ProcessRunner { get; set; }

        public event EventHandler<ICollection<PSObject>> DataReady;

        public event EventHandler<ICollection<object>> ErrorReady;

        public event EventHandler<InformationRecord> Info;

        public event EventHandler<VerboseRecord> Verbose;

        public event EventHandler<WarningRecord> Warning;

        public event EventHandler<DebugRecord> Debug;

        public void RunScript(IReadOnlyCollection<string> scripts, Dictionary<string, object> variables, CancellationToken cancellationToken = default(CancellationToken)) {
            using (PowerShell shell = PowerShell.Create()) {
                InvokePipeline(scripts, variables, shell, cancellationToken);
            }
        }

        private void InvokePipeline(IReadOnlyCollection<string> scripts, Dictionary<string, object> variables, PowerShell shell, CancellationToken cancellationToken) {
            using (var runspace = RunspaceFactory.CreateRunspace()) {
                runspace.Open();

                shell.Runspace = runspace;

                SetExecutionPolicy(shell);
                SetProgressPreference(shell);

                if (ProcessRunner != null) {
                    runspace.SessionStateProxy.SetVariable("exec", ProcessRunner.StartProcess);
                }

                if (variables != null) {
                    foreach (var variable in variables) {
                        runspace.SessionStateProxy.SetVariable(variable.Key, variable.Value);
                    }
                }

                try {
                    using (var pipeline = runspace.CreatePipeline()) {
                        cancellationToken.Register(shell.Stop);

                        foreach (var script in scripts) {
                            var command = new Command(script, true);

                            command.MergeMyResults(PipelineResultTypes.Null, PipelineResultTypes.Output);
                            command.MergeMyResults(PipelineResultTypes.Debug, PipelineResultTypes.Output);
                            command.MergeMyResults(PipelineResultTypes.Warning, PipelineResultTypes.Output);
                            command.MergeMyResults(PipelineResultTypes.Debug, PipelineResultTypes.Output);
                            command.MergeMyResults(PipelineResultTypes.Verbose, PipelineResultTypes.Output);
                            command.MergeMyResults(PipelineResultTypes.Information, PipelineResultTypes.Output);

                            pipeline.Commands.Add(command);
                        }

                        pipeline.Output.DataReady += HandleDataReady;
                        pipeline.Error.DataReady += HandleErrorReady;

                        try {
                            var result = pipeline.Invoke();

                            if (result != null && result.Count > 0) {
                                Result = result.Select(s => s.ToString()).ToList();
                            }
                        } finally {
                            HadErrors = pipeline.HadErrors;

                            pipeline.Output.DataReady -= HandleDataReady;
                            pipeline.Error.DataReady -= HandleErrorReady;

                            if (HadErrors) {
                                var errorArray = runspace.SessionStateProxy.GetVariable("Error") as ICollection;

                                if (errorArray != null) {
                                    foreach (var error in errorArray) {
                                        // All of the errors should be ErrorRecords but on some installs we've seen this
                                        // Unable to cast object of type 'System.Management.Automation.ParameterBindingException' to type 'System.Management.Automation.ErrorRecord'.
                                        ErrorRecord record = error as ErrorRecord;

                                        if (record != null && record.Exception != null) {
                                            throw record.Exception;
                                        }

                                        var ex = error as Exception;
                                        if (ex != null) {
                                            throw ex;
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch (ParseException ex) {
                    // This should only happen in case of script syntax errors
                    if (ErrorReady != null) {
                        ErrorReady(this, new Collection<object> { ex.Message });
                    }
                }
            }
        }

        private void HandleErrorReady(object sender, EventArgs e) {
            var reader = sender as PipelineReader<object>;

            if (reader != null) {
                while (reader.Count > 0) {

                    var item = reader.Read();

                    if (item != null) {

                        if (ErrorReady != null) {
                            ErrorReady(this, new[] { item });
                        }
                    }
                }
            }
        }

        private void HandleDataReady(object sender, EventArgs e) {
            PipelineReader<PSObject> reader = sender as PipelineReader<PSObject>;

            if (reader != null) {
                while (reader.Count > 0) {
                    var item = reader.Read();

                    if (item != null) {
                        WarningRecord warningRecord = item.BaseObject as WarningRecord;
                        if (warningRecord != null) {
                            OnWarning(warningRecord);
                            continue;
                        }

                        var verboseRecord = item.BaseObject as VerboseRecord;
                        if (verboseRecord != null) {
                            OnVerbose(verboseRecord);
                            continue;
                        }

                        var debugRecord = item.BaseObject as DebugRecord;
                        if (debugRecord != null) {
                            OnDebug(debugRecord);
                            continue;
                        }

                        var informationRecord = item.BaseObject as InformationRecord;
                        if (informationRecord != null) {
                            OnInfo(informationRecord);
                            continue;
                        }

                        if (Result == null) {
                            Result = new List<string>();
                        }

                        Result.Add(item.ToString());

                        if (DataReady != null) {
                            DataReady(this, new[] { item });
                        }
                    }
                }
            }
        }

        private static void SetExecutionPolicy(PowerShell shell) {
            // ensure execution policy will allow the script to run
            shell.AddCommand("Set-ExecutionPolicy")
                .AddParameter("ExecutionPolicy", "Unrestricted")
                .AddParameter("Scope", "Process")
                .AddParameter("Force")
                .Invoke();
        }

        private void SetProgressPreference(PowerShell shell) {
            if (!string.IsNullOrWhiteSpace(ProgressPreference)) {
                shell.AddScript($"$ProgressPreference = '{ProgressPreference}'");
            }
        }

        private void OnVerbose(VerboseRecord e) {
            Verbose?.Invoke(this, e);
        }

        private void OnDebug(DebugRecord e) {
            Debug?.Invoke(this, e);
        }

        private void OnWarning(WarningRecord e) {
            Warning?.Invoke(this, e);
        }

        private void OnInfo(InformationRecord e) {
            Info?.Invoke(this, e);
        }
    }
}
