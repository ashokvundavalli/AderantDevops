using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {
    internal struct SolutionProject {

        public SolutionProject(string file, ProjectInSolution project) {
            SolutionFile = file;
            Project = project;
        }

        /// <summary>
        /// Gets the solution file path.
        /// </summary>
        /// <value>The solution file.</value>
        public string SolutionFile { get; private set; }

        /// <summary>
        /// Gets the project referenced by the solution at <see cref="SolutionFile" />
        /// </summary>
        /// <value>The project.</value>
        public ProjectInSolution Project { get; private set; }
    }
}
