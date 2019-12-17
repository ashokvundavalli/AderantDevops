using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class DependencyChecker : Microsoft.Build.Utilities.Task {

        [Required]
        public ITaskItem[] Files { get; set; }

        public override bool Execute() {
            IEnumerable<IGrouping<string, string>> groupings = Files
                .Select(s => s.ItemSpec)
                .GroupBy(g => Path.GetFileName(g))
                .Where(g => g.Count() > 1)
                .ToList();

            if (groupings.Any()) {
                Log.LogError("Duplicate files found. The build does not support multiple versions of the same file. Consolidate the dependencies to a single file and version.");
            }


            foreach (var grouping in groupings) {
                string duplicateMessage = "Duplicate file name: " + grouping.Key;

                Log.LogMessage(new string('=', duplicateMessage.Length));
                Log.LogMessage(duplicateMessage, null);
                Log.LogMessage(new string('=', duplicateMessage.Length));


                foreach (string file in grouping) {
                    if (!GetVersionInfo(file)) {
                        Log.LogMessage(file);
                    }
                }
            }

            Log.LogMessage("No duplicate dependencies found.", null);
            return !Log.HasLoggedErrors;
        }

        private bool GetVersionInfo(string file) {
            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file);

                string message = versionInfo.ToString();
                Log.LogMessage(message);

                return true;
            }
            return false;
        }
    }
}