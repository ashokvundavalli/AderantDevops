using System;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public class FilterModules : Microsoft.Build.Utilities.Task {

        [Required]
        [Output]
        public ITaskItem[] ModulesInBuild { get; set; }

        [Required]
        public string BuildFrom { get; set; }

        public override bool Execute() {
            // Finds the ordinal position of the module in a sorted list and returns that specific module and all modules that follow it

            if (!string.IsNullOrEmpty(BuildFrom)) {
                int i;

                BuildFrom = BuildFrom.Trim(null);
                for (i = 0; i < ModulesInBuild.Length; i++) {
                    ITaskItem item = ModulesInBuild[i];

                    // Convert $(SolutionRoot)\Modules\Libraries.Models into Libraries.Models
                    string name = System.IO.Path.GetFileName(item.ItemSpec);
                    if (name != null) {
                        name = name.Trim(null);

                        if (string.Equals(name, BuildFrom, StringComparison.OrdinalIgnoreCase)) {
                            Log.LogMessage("Found module {0} to build from", item.ItemSpec);
                            break;
                        }
                    }
                }

                // If we didn't find a match return the full list
                if (i < ModulesInBuild.Length) {
                    ModulesInBuild = ModulesInBuild.Skip(i).ToArray();
                } else {
                    Log.LogMessage("Did not find a matching module to build from", null);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}