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

        public bool Output { get; set; }

        public override bool Execute() {
            if (Output) {
                FileVersion = Context.GetVariableFromBag(Id, nameof(FileVersion));
                AssemblyVersion = Context.GetVariableFromBag(Id, nameof(AssemblyVersion));
                ModuleName = Context.GetVariableFromBag(Id, nameof(ModuleName));
            } else {
                Context.AddVariableToBag(Id, nameof(FileVersion), FileVersion);
                Context.AddVariableToBag(Id, nameof(AssemblyVersion), AssemblyVersion);
                Context.AddVariableToBag(Id, nameof(ModuleName), ModuleName);
                ReplaceContext();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
