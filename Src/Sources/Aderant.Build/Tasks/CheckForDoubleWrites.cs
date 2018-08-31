using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Fails if any double writes are detected.
    /// </summary>
    public class CheckForDoubleWrites : Task {

        [Required]
        public string[] FileList { get; set; }

        public override bool Execute() {
            DuplicateFileCheck.CheckForDoubleWrites(FileList);

            return !Log.HasLoggedErrors;
        }
    }
}
