using System.Collections.Generic;

namespace Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer {
    public class AnalyzerContext {
        public ICollection<string> Directories { get; set; } = new List<string>();

        public AnalyzerContext AddDirectory(string directory) {
            Directories.Add(directory);

            return this;
        }
    }
}