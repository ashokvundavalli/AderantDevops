using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class PushPaketTemplatesTask : Microsoft.Build.Utilities.Task {

        /// <summary>
        /// Gets or sets the root folder.
        /// </summary>
        [Required]
        public string RootFolder { get; set; }

        /// <summary>
        /// Gets or sets the build scripts directory.
        /// </summary>
        [Required]
        public string BuildScriptsDirectory { get; set; }


        public override bool Execute() {
            var logger = new BuildTaskLogger(this);

            var action = new PushPaketTemplatesAction();
            return action.Execute(logger, BuildScriptsDirectory, RootFolder, executeInParallel: true);

        }
    }
}