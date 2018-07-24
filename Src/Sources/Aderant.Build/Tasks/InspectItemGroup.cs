using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class InspectItemGroup : Task {

        [Required]
        public ITaskItem[] ItemGroup { get; set; }

        public override bool Execute() {
            return !Log.HasLoggedErrors;
        }
    }
}
