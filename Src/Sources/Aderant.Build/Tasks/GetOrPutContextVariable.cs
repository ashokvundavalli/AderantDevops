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
                FileVersion = ContextService.GetVariable(Id, nameof(FileVersion));
                AssemblyVersion = ContextService.GetVariable(Id, nameof(AssemblyVersion));
                ModuleName = ContextService.GetVariable(Id, nameof(ModuleName));
            } else {
                Log.LogMessage("FileVersion: {0}", FileVersion);
                Log.LogMessage("AssemblyVersion: {0}", AssemblyVersion);
                Log.LogMessage("ModuleName: {0}", ModuleName);

                ContextService.PutVariable(Id, nameof(FileVersion), FileVersion);
                ContextService.PutVariable(Id, nameof(AssemblyVersion), AssemblyVersion);
                ContextService.PutVariable(Id, nameof(ModuleName), ModuleName);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
