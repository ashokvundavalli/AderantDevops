using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class PowerShellScript : Task {

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

            pipelineExecutor.RunScript(ScriptBlock).Wait();

            return !Log.HasLoggedErrors;
        }
    }
}
