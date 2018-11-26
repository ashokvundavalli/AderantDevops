using System;
using System.Collections.Generic;
using System.IO;
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

        public bool CheckFileSize { get; set; }

        public override bool Execute() {
            List<PathSpec> paths = new List<PathSpec>(FileList.Length);

            foreach (ITaskItem taskItem in FileList) {
                string metadata = taskItem.GetMetadata("OriginalItemSpec");

                ErrorUtilities.VerifyThrowArgument(metadata.Length != 0, "CheckForDoubleWrites.FileList has task item {0} is missing OriginalItemSpec metadata.", taskItem.ItemSpec);

                paths.Add(new PathSpec(metadata, taskItem.ItemSpec));
            }

            try {
                var checker = new DoubleWriteCheck(s => new FileInfo(s));

                checker.CheckFileSize = CheckFileSize;
                checker.CheckForDoubleWrites(paths);

                if (checker.IgnoredDoubleWrites != null && checker.IgnoredDoubleWrites.Count > 0) {
                    Log.LogMessage("Ignored double writes");
                    foreach (PathSpec pathSpec in checker.IgnoredDoubleWrites) {
                        Log.LogMessage("{0} -> {1}", pathSpec.Location, pathSpec.Destination);
                    }
                }
            } catch (Exception exception) {
                Log.LogErrorFromException(exception);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
