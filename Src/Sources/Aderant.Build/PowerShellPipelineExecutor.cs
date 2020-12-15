using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
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

        internal IFileSystem FileSystem;

        public PowerShellPipelineExecutor() : this(new PhysicalFileSystem()) {
        }

        internal PowerShellPipelineExecutor(IFileSystem fileSystem) {
            this.FileSystem = fileSystem;
        }

        public void RunScript(PSCommand command, Dictionary<string, object> variables, CancellationToken cancellationToken = default(CancellationToken)) {
            RunScript(command, variables, null, cancellationToken);
        }

        public void RunScript(PSCommand command, Dictionary<string, object> variables, string workingDirectory, CancellationToken cancellationToken = default(CancellationToken)) {
            using (PowerShell shell = PowerShell.Create()) {
                using (cancellationToken.Register(shell.Stop)) {
                    InvokePipeline(command, variables, shell, workingDirectory);
                }
            }
        }

        private void InvokePipeline(PSCommand command, Dictionary<string, object> variables, PowerShell shell, string workingDirectory) {
            Collection<PSObject> result = new Collection<PSObject>();

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

                if (!string.IsNullOrWhiteSpace(workingDirectory)) {
                    // The working directory cannot be set if it does not exist.
                    if (!FileSystem.DirectoryExists(workingDirectory)) {
                        FileSystem.CreateDirectory(workingDirectory);
                    }

                    runspace.SessionStateProxy.Path.SetLocation(workingDirectory);
                }

                Pipeline pipeline = null;

                try {
                    foreach (var cmd in command.Commands) {
                        if (pipeline == null) {
                            pipeline = runspace.CreatePipeline();
                        }

                        cmd.MergeMyResults(PipelineResultTypes.Null, PipelineResultTypes.Output);
                        cmd.MergeMyResults(PipelineResultTypes.Debug, PipelineResultTypes.Output);
                        cmd.MergeMyResults(PipelineResultTypes.Warning, PipelineResultTypes.Output);
                        cmd.MergeMyResults(PipelineResultTypes.Debug, PipelineResultTypes.Output);
                        cmd.MergeMyResults(PipelineResultTypes.Verbose, PipelineResultTypes.Output);
                        cmd.MergeMyResults(PipelineResultTypes.Information, PipelineResultTypes.Output);

                        pipeline.Commands.Add(cmd);

                        // If the runspace is remote PowerShell will batch the pipeline - executing now will cause the pipeline to hang
                        if (!runspace.RunspaceIsRemote) {
                            if (cmd.IsEndOfStatement) {
                                // EndOfStatement must execute now.
                                // For example if you define a function and then want to call it immediately such as
                                // function foo() | foo
                                // Alternatively define two scripts
                                InvokePipeline(pipeline, result);

                                // Remove pipeline as double execution is forbidden
                                pipeline.Dispose();
                                pipeline = null;

                                if (command.Commands.Count == 1) {
                                    return;
                                }
                            }
                        }
                    }

                    if (pipeline != null) {
                        InvokePipeline(pipeline, result);
                    }
                } catch (ParseException ex) {
                    // This should only happen in case of script syntax errors
                    RaiseError(ex.Message);
                } finally {
                    if (result.Count > 0) {
                        Result = result.Select(o => o.ToString()).ToList();
                    }

                    if (pipeline != null) {
                        pipeline.Dispose();
                    }
                }
            }
        }

        private void InvokePipeline(Pipeline pipeline, Collection<PSObject> result) {
            pipeline.Output.DataReady += HandleDataReady;
            pipeline.Error.DataReady += HandleErrorReady;

            try {
                var results = pipeline.Invoke();

                foreach (PSObject outputItem in results) {
                    result.Add(outputItem);
                }
            } finally {
                HadErrors = pipeline.HadErrors;

                pipeline.Output.DataReady -= HandleDataReady;
                pipeline.Error.DataReady -= HandleErrorReady;

                if (HadErrors) {
                    var errorArray = pipeline.Runspace.SessionStateProxy.GetVariable("Error") as ICollection;

                    if (errorArray != null) {
                        foreach (var error in errorArray) {
                            // All of the errors should be ErrorRecords but on some installs we've seen this
                            // Unable to cast object of type 'System.Management.Automation.ParameterBindingException' to type 'System.Management.Automation.ErrorRecord'.
                            ErrorRecord record = error as ErrorRecord;

                            if (record != null && record.Exception != null) {
                                string errorString = ErrorRecordToString(record);

                                RaiseError(errorString);

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

        private void RaiseError(object obj) {
            var handler = ErrorReady;
            if (handler != null) {
                handler(this, new Collection<object> {obj});
            }
        }

        private static string ErrorRecordToString(ErrorRecord error) {
            StringBuilder sb = new StringBuilder(error.ToString());

            if (error.ScriptStackTrace != null) {
                sb.AppendLine("");
                sb.AppendLine(error.ScriptStackTrace);
            }

            if (error.Exception != null && error.Exception.StackTrace != null) {
                sb.AppendLine("");
                sb.AppendLine(error.Exception.StackTrace);
            }

            return sb.ToString();
        }

        private void HandleErrorReady(object sender, EventArgs e) {
            var reader = sender as PipelineReader<object>;

            if (reader != null) {
                while (reader.Count > 0) {
                    var item = reader.Read();

                    if (item != null) {
                        RaiseError(item);
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
                            DataReady(this, new[] {item});
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