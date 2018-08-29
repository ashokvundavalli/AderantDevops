using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class PowerShellScript : Task {

        [Required]
        public string ScriptBlock { get; private set; }

        public override bool Execute() {
        
            var pipelineExecutor = new PowerShellPipelineExecutor();
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
