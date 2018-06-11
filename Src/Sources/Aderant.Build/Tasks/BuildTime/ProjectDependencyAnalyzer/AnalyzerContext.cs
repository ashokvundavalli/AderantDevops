using System.Collections.Generic;

namespace Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer {
    public class AnalyzerContext {
        public ICollection<string> Directories { get; set; } = new List<string>();

        /// <summary>
        /// To pass the needed files list.
        /// </summary>
        public ICollection<string> Files { get; set; } = new List<string>();

        public IEnumerable<string> ProjectFiles { get; set; }

        /// <summary>
        /// The root directory of the current build.
        /// </summary>
        public string ModulesDirectory { get; set; }

        public AnalyzerContext AddDirectory(string directory) {
            Directories.Add(directory);

            return this;
        }

        public AnalyzerContext AddFile(string file) {
            Files.Add(file);

            return this;
        }
        public AnalyzerContext SetFilesList(List<string> filesList) {
            Files = filesList;

            return this;
        }
    }
}