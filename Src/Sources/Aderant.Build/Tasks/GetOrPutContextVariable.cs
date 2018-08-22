using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GetOrPutContextVariable : BuildOperationContextTask {

        [Required]
        public string Scope { get; set; }

        [Output]
        public string FileVersion { get; set; }

        [Output]
        public string AssemblyVersion { get; set; }

        [Output]
        public string ModuleName { get; set; }

        public bool Output { get; set; }

        public override bool ExecuteTask() {
            if (Output) {
                FileVersion = PipelineService.GetVariable(Scope, nameof(FileVersion));
                AssemblyVersion = PipelineService.GetVariable(Scope, nameof(AssemblyVersion));
                ModuleName = PipelineService.GetVariable(Scope, nameof(ModuleName));
            } else {
                Log.LogMessage("FileVersion: {0}", FileVersion);
                Log.LogMessage("AssemblyVersion: {0}", AssemblyVersion);
                Log.LogMessage("ModuleName: {0}", ModuleName);

                PipelineService.PutVariable(Scope, nameof(FileVersion), FileVersion);
                PipelineService.PutVariable(Scope, nameof(AssemblyVersion), AssemblyVersion);
                PipelineService.PutVariable(Scope, nameof(ModuleName), ModuleName);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
