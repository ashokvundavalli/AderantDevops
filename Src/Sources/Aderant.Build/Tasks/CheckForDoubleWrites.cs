using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Fails if any double writes are detected.
    /// Confirms that two or more files do not write to the same destination. This ensures deterministic behavior.
    /// </summary>
    public class CheckForDoubleWrites : Task {

        [Required]
        public ITaskItem[] FileList { get; set; }

        public override bool Execute() {
            List<PathSpec> paths = new List<PathSpec>(FileList.Length);

            foreach (ITaskItem taskItem in FileList) {
                string metadata = taskItem.GetMetadata("OriginalItemSpec");

                ErrorUtilities.VerifyThrowArgument(metadata.Length != 0, "CheckForDoubleWrites.FileList has task item {0} is missing OriginalItemSpec metadata.", taskItem.ItemSpec);

                paths.Add(new PathSpec(metadata, taskItem.ItemSpec));
            }

            try {
                DoubleWriteCheck.CheckForDoubleWrites(paths);
            } catch (Exception exception) {
                Log.LogErrorFromException(exception);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
