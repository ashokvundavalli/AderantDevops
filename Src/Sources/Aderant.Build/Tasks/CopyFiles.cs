using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Similar in spirit to the Copy task provided by MSBuild but uses an action block for parallel IO for improved performance.
    /// </summary>
    public class CopyFiles : Task {
        private PhysicalFileSystem fileSystem;

        public CopyFiles() {
            fileSystem = new PhysicalFileSystem();
        }

        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        public ITaskItem DestinationFolder { get; set; }

        [Output]
        public ITaskItem[] DestinationFiles { get; set; }

        public bool Overwrite { get; set; }

        public override bool Execute() {
            if (SourceFiles == null || SourceFiles.Length == 0) {
                DestinationFiles = new TaskItem[0];
                return true;
            }

            if (!ValidateInputs()) {
                return false;
            }

            List<PathSpec> copySpecs = new List<PathSpec>();

            var seenDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < SourceFiles.Length; i++) {
                var sourceFile = SourceFiles[i];
                var destinationFile = DestinationFiles[i];

                var add = seenDestinations.Add(destinationFile.ItemSpec);
                if (add) {
                    var copyItem = new PathSpec(sourceFile.ItemSpec, destinationFile.ItemSpec);
                    copySpecs.Add(copyItem);
                } else {
                    Log.LogWarning("The file {0} -> {1} was ignored as it would result in a double write.", sourceFile.ItemSpec, destinationFile.ItemSpec);
                }
            }

            ActionBlock<PathSpec> bulkCopy = fileSystem.BulkCopy(copySpecs, Overwrite);

            bulkCopy.Completion.GetAwaiter().GetResult();

            return !Log.HasLoggedErrors;
        }

        private bool ValidateInputs() {
            if (DestinationFiles != null && DestinationFolder != null) {
                Log.LogError("Exactly one type of destination should be provided.");
                return false;
            }

            if (DestinationFiles != null && DestinationFiles.Length != SourceFiles.Length) {
                // The two vectors must have the same length
                Log.LogError(
                    "{2} refers to {0} item(s), and {3} refers to {1} item(s).They must have the same number of items.",
                    DestinationFiles.Length,
                    SourceFiles.Length,
                    "DestinationFiles",
                    "SourceFiles");
                return false;
            }

            return true;
        }
    }
}
