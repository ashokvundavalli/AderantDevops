using System.Collections.Generic;
using Aderant.Build.VersionControl;

namespace Aderant.Build.DependencyAnalyzer {
    public class AnalyzerContext {
        public ICollection<string> Directories { get; set; } = new List<string>();

        /// <summary>
        /// To pass the needed files list.
        /// </summary>
        public IEnumerable<IPendingChange> PendingChanges { get; set; } = new List<IPendingChange>();

        public IEnumerable<string> ProjectFiles { get; set; }

        /// <summary>
        /// The root directory of the current build.
        /// </summary>
        public string ModulesDirectory { get; set; }

        public AnalyzerContext AddDirectory(string directory) {
            Directories.Add(directory);

            return this;
        }
    }
}
