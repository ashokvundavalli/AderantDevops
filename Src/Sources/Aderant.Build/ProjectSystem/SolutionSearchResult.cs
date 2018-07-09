using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// The results of a solution search.
    /// </summary>
    internal struct SolutionSearchResult {

        public SolutionSearchResult(string file, ProjectInSolution project) {
            SolutionFile = file;
            Project = project;
            Found = true;
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

        /// <summary>
        /// Gets or sets a value indicating whether a solution was found.
        /// </summary>
        public bool Found { get; internal set; }
    }
}
