using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class UpdateVariableBag : ContextTaskBase {

        [Required]
        public string Id { get; set; }

        [Output]
        public string FileVersion { get; set; }

        [Output]
        public string AssemblyVersion { get; set; }

        [Output]
        public string ModuleName { get; set; }

      [Output]
        public string _FileVersion { get; set; }

        [Output]
        public string _AssemblyVersion { get; set; }

        [Output]
        public string _ModuleName { get; set; }

        public bool Output { get; set; }

        public override bool Execute() {
            if (Output) {
                _FileVersion = Context.GetVariableFromBag(Id, nameof(FileVersion));
                _AssemblyVersion = Context.GetVariableFromBag(Id, nameof(AssemblyVersion));
                _ModuleName = Context.GetVariableFromBag(Id, nameof(ModuleName));
            } else {
                Log.LogMessage("FileVersion: {0}", FileVersion);
                Log.LogMessage("AssemblyVersion: {0}", AssemblyVersion);
                Log.LogMessage("ModuleName: {0}", ModuleName);

                Context.AddVariableToBag(Id, nameof(FileVersion), FileVersion);
                Context.AddVariableToBag(Id, nameof(AssemblyVersion), AssemblyVersion);
                Context.AddVariableToBag(Id, nameof(ModuleName), ModuleName);
                ReplaceContext();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
