using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Aderant.Build {
    internal class PowerShellPipelineExecutor {
        public string ProgressPreference { get; set; }

        /// <summary>
        /// Indicates you want the script invoked with Measure-Command
        /// </summary>
        public bool MeasureCommand { get; set; }

        /// <summary>
        /// The scalar script result.
        /// </summary>
        public string Result { get; set; }

        public event EventHandler<string> Error;

        public event EventHandler<string> Verbose;

        public event EventHandler<string> Warning;

        public event EventHandler<string> Debug;

        public event EventHandler<string> Output;

        public async Task RunScript(string script) {

            // create a new runspace to isolate the scripts
            using (var runspace = RunspaceFactory.CreateRunspace()) {
                runspace.Open();

                using (PowerShell shell = PowerShell.Create()) {
                    shell.Runspace = runspace;

                    // ensure execution policy will allow the script to run
                    shell.AddCommand("Set-ExecutionPolicy")
                        .AddParameter("ExecutionPolicy", "Unrestricted")
                        .AddParameter("Scope", "Process")
                        .AddParameter("Force")
                        .Invoke();

                    if (!string.IsNullOrWhiteSpace(ProgressPreference)) {
                        shell.AddScript($"$ProgressPreference = '{ProgressPreference}'");
                    }

                    if (MeasureCommand) {
                        shell.Commands.AddScript("Measure-Command {" + script + "}");
                    } else {
                        shell.Commands.AddScript(script);
                    }

                    // capture errors
                    shell.Streams.Error.DataAdded += (sender, args) => {
                        var error = shell.Streams.Error[args.Index];
                        string src = error.InvocationInfo.MyCommand?.ToString() ?? error.InvocationInfo.InvocationName;
                        OnError($"{src}: {error}\n{error.InvocationInfo.PositionMessage}");
                    };

                    // capture write-* methods (except write-host)
                    shell.Streams.Warning.DataAdded += (sender, args) => OnWarning(shell.Streams.Warning[args.Index].Message);
                    shell.Streams.Debug.DataAdded += (sender, args) => OnDebug(shell.Streams.Debug[args.Index].Message);
                    shell.Streams.Verbose.DataAdded += (sender, args) => OnVerbose(shell.Streams.Verbose[args.Index].Message);

                    var outputData = new PSDataCollection<PSObject>();
                    outputData.DataAdded += (sender, args) => {
                        // capture all main output
                        object data = outputData[args.Index]?.BaseObject;
                        if (data != null) {
                            OnOutput(data.ToString());
                        }
                    };

                    await Task.Run(
                        () => {
                            try {
                                var async = shell.BeginInvoke<PSObject, PSObject>(null, outputData);
                                PSDataCollection<PSObject> results = shell.EndInvoke(async);

                                Result = outputData.FirstOrDefault()?.ToString();
                            } catch (ParseException ex) {
                                // this should only happen in case of script syntax errors, otherwise
                                // errors would be output via the invoke's error stream 
                                OnError($"{ex.Message}");
                            }
                        });

                }
            }

        }

        private void OnOutput(string e) {
            Output?.Invoke(this, e);
        }

        private void OnVerbose(string e) {
            Verbose?.Invoke(this, e);
        }

        private void OnDebug(string e) {
            Debug?.Invoke(this, e);
        }

        private void OnWarning(string e) {
            Warning?.Invoke(this, e);
        }

        protected virtual void OnError(string e) {
            Error?.Invoke(this, e);
        }
    }
}
