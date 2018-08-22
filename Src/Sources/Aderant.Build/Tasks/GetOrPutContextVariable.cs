using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GetOrPutContextVariable : BuildOperationContextTask {

        [Required]
        public string Id { get; set; }

        [Output]
        public string FileVersion { get; set; }

        [Output]
        public string AssemblyVersion { get; set; }

        [Output]
        public string ModuleName { get; set; }

        public bool Output { get; set; }

        public override bool ExecuteTask() {
            if (Output) {
                FileVersion = PipelineService.GetVariable(Id, nameof(FileVersion));
                AssemblyVersion = PipelineService.GetVariable(Id, nameof(AssemblyVersion));
                ModuleName = PipelineService.GetVariable(Id, nameof(ModuleName));
            } else {
                Log.LogMessage("FileVersion: {0}", FileVersion);
                Log.LogMessage("AssemblyVersion: {0}", AssemblyVersion);
                Log.LogMessage("ModuleName: {0}", ModuleName);

                PipelineService.PutVariable(Id, nameof(FileVersion), FileVersion);
                PipelineService.PutVariable(Id, nameof(AssemblyVersion), AssemblyVersion);
                PipelineService.PutVariable(Id, nameof(ModuleName), ModuleName);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
