using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using ILogger = Aderant.Build.Logging.ILogger;

namespace Aderant.BuildTime.Tasks {
    internal sealed class PowerShellScriptRunner {
        private readonly ILogger logger;

        public bool ErrorStreamToOutputStream { get; set; }

        private Collection<PSObject> output = new Collection<PSObject>();
        private Runspace runspace;
        private PowerShell ps;

        public event EventHandler<ScriptCompletedEventArgs> ScriptCompleted;

        public PowerShellScriptRunner(Aderant.Build.Logging.ILogger logger) {
            this.logger = logger;
            this.runspace = RunspaceFactory.CreateRunspace(new BuildMasterPSHost(logger));
            
            runspace.Open();

            this.ps = PowerShell.Create();
            ps.Runspace = runspace;
            ps.Streams.Debug.DataAdded += OnDebugOnDataAdded;
        }

        private void OnDebugOnDataAdded(object sender, DataAddedEventArgs args) {
            
        }

        public void ExecuteCommand(string command) {
            logger.Info("Invoking PowerShell command: " + command);

            using (Pipeline pipeline = ps.Runspace.CreatePipeline()) {
                pipeline.Commands.AddScript("$DebugPreference = 'Continue'");

                // NewLine allows for beautiful formatting in build scripts but we need everything
                // on a single line for invocation
                pipeline.Commands.AddScript(command.Replace(Environment.NewLine, " "));

                pipeline.Output.DataReady += Output_DataReady;
                pipeline.Error.DataReady += Error_DataReady;

                pipeline.Invoke();

                OnScriptCompleted(new ScriptCompletedEventArgs(pipeline.Runspace));
            }
        }

        public void ExecuteScript(string scriptText, string arguments) {
            using (Pipeline pipeline = ps.Runspace.CreatePipeline()) {
                pipeline.Commands.AddScript("$DebugPreference = 'Continue'");

                pipeline.Commands.AddScript(scriptText);
                pipeline.Commands.AddScript(arguments);

                pipeline.Output.DataReady += Output_DataReady;
                pipeline.Error.DataReady += Error_DataReady;

                pipeline.Invoke();

                OnScriptCompleted(new ScriptCompletedEventArgs(pipeline.Runspace));
            }
        }

        public IEnumerable<string> Output {
            get {
                foreach (var item in output) {
                    yield return item.ToString();
                }
            }
        }

        private void Output_DataReady(object sender, EventArgs e) {
            var output = sender as PipelineReader<PSObject>;

            if (output == null)
                return;

            while (output.Count > 0) {
                var outputItem = output.Read();
                if (logger != null) {
                    logger.Info(outputItem.ToString(), null);
                }

                this.output.Add(outputItem);
            }
        }

        private void Error_DataReady(object sender, EventArgs e) {
            var error = sender as PipelineReader<object>;

            if (error == null) {
                return;
            }

            while (error.Count > 0) {
                var errorItem = error.Read();
                var pso = new PSObject(errorItem);

                if (ErrorStreamToOutputStream) {
                    logger.Info(pso.ToString(), null);
                } else {
                    logger.Error(pso.ToString(), null);
                }
            }
        }

        public void CaptureHostOutput() {
        }

        public void SetGlobals(IDictionary<string, object> globals) {
            foreach (var global in globals) {
                runspace.SessionStateProxy.SetVariable(global.Key, global.Value);
            }
        }

        private void OnScriptCompleted(ScriptCompletedEventArgs e) {
            ScriptCompleted?.Invoke(this, e);
        }
    }

    internal class ScriptCompletedEventArgs {
        public Runspace Runspace { get; }

        public ScriptCompletedEventArgs(Runspace runspace) {
            Runspace = runspace;
        }
    }
}