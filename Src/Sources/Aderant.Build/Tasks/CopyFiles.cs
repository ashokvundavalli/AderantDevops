using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Similar in spirit to the Copy task provided by MSBuild but uses an action block for parallel IO for improved
    /// performance.
    /// </summary>
    public class CopyFiles : Task {
        private IFileSystem fileSystem;

        public CopyFiles() : this(null) {
        }

        internal CopyFiles(IFileSystem fileSystem) {
            this.fileSystem = fileSystem ?? new PhysicalFileSystem(null, new BuildTaskLogger(Log));
        }

        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        public ITaskItem DestinationFolder { get; set; }

        [Output]
        public ITaskItem[] DestinationFiles { get; set; }

        public bool Overwrite { get; set; }

        public bool UseSymlinks { get; set; }

        public bool UseHardlinks { get; set; }

        public bool UseHardlinksIfPossible {
            get { return UseHardlinks; }
            set { UseHardlinks = value; }
        }

        /// <summary>
        /// Provides API compatibly with Copy task
        /// </summary>
        public bool SkipUnchangedFiles { get; set; }

        /// <summary>
        /// Provides API compatibly with Copy task
        /// </summary>
        public bool OverwriteReadOnlyFiles { get; set; }

        /// <summary>
        /// Provides API compatibly with Copy task
        /// </summary>
        public int Retries { get; set; }

        /// <summary>
        /// Provides API compatibly with Copy task
        /// </summary>
        public int RetryDelayMilliseconds { get; set; }

        public override bool Execute() {
            if (SourceFiles == null || SourceFiles.Length == 0) {
                DestinationFiles = new ITaskItem[0];
                return true;
            }

            if (!ValidateInputs() || !InitializeDestinationFiles()) {
                return false;
            }

            try {
                BuildEngine4.Yield();

                List<PathSpec> copySpecs = new List<PathSpec>();

                var seenDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < SourceFiles.Length; i++) {
                    var sourceFile = SourceFiles[i];
                    var destinationFile = DestinationFiles[i];

                    var add = seenDestinations.Add(destinationFile.ItemSpec);
                    if (add) {
                        var copyItem = new PathSpec(sourceFile.ItemSpec, destinationFile.GetMetadata("FullPath"));
                        copySpecs.Add(copyItem);
                    } else {
                        Log.LogWarning("The file {0} -> {1} was ignored as it would result in a double write.", sourceFile.ItemSpec, destinationFile.ItemSpec);
                    }
                }

                ActionBlock<PathSpec> bulkCopy = fileSystem.BulkCopy(copySpecs, Overwrite, UseSymlinks, UseHardlinks);

                // Unit testing guard
                if (bulkCopy != null) {
                    bulkCopy
                        .Completion
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
            } finally {
                BuildEngine4.Reacquire();
            }

            return !Log.HasLoggedErrors;
        }

        private bool InitializeDestinationFiles() {
            if (DestinationFiles == null) {
                DestinationFiles = new ITaskItem[SourceFiles.Length];

                for (int i = 0; i < SourceFiles.Length; i++) {
                    string unescapedString;
                    try {
                        unescapedString = Path.Combine(DestinationFolder.ItemSpec, Path.GetFileName(SourceFiles[i].ItemSpec));
                    } catch (ArgumentException ex) {
                        Log.LogError("Unable to copy file \"{0}\" to \"{1}\". {2}", SourceFiles[i].ItemSpec, DestinationFolder.ItemSpec, ex.Message);
                        DestinationFiles = new ITaskItem[0];
                        return false;
                    }

                    DestinationFiles[i] = new TaskItem(ProjectCollection.Escape(unescapedString));
                    SourceFiles[i].CopyMetadataTo(DestinationFiles[i]);
                }
            }

            return true;
        }

        private bool ValidateInputs() {
            if (DestinationFiles != null && DestinationFolder != null) {
                Log.LogError("Exactly one type of destination should be provided.");
                return false;
            }

            if (DestinationFiles != null && DestinationFiles.Length != SourceFiles.Length) {
                // The two vectors must have the same length
                Log.LogError(
                    "{2} refers to {0} item(s), and {3} refers to {1} item(s). They must have the same number of items.",
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