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

        public override bool Execute() {
            if (Output) {
                FileVersion = Context.GetVariable(Id, nameof(FileVersion));
                AssemblyVersion = Context.GetVariable(Id, nameof(AssemblyVersion));
                ModuleName = Context.GetVariable(Id, nameof(ModuleName));
            } else {
                Log.LogMessage("FileVersion: {0}", FileVersion);
                Log.LogMessage("AssemblyVersion: {0}", AssemblyVersion);
                Log.LogMessage("ModuleName: {0}", ModuleName);

                Context.PutVariable(Id, nameof(FileVersion), FileVersion);
                Context.PutVariable(Id, nameof(AssemblyVersion), AssemblyVersion);
                Context.PutVariable(Id, nameof(ModuleName), ModuleName);
                UpdateContext();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
