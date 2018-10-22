using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem {
    internal class AnalysisContext {

        /// <summary>
        /// Gets or sets the paths we should not enter when grovelling for files.
        /// </summary>
        public IReadOnlyCollection<string> ExcludePaths { get; set; }

        /// <summary>
        /// Gets or sets the extensibility demands such as things we should always build
        /// </summary>
        public ExtensibilityImposition ExtensibilityImposition { get; set; }
    }
}
