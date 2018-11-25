using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem {
    internal class AnalysisContext {

        /// <summary>
        /// Gets or sets the paths we should not enter when grovelling for files.
        /// </summary>
        public IReadOnlyCollection<string> ExcludePaths { get; set; }
    }
}
