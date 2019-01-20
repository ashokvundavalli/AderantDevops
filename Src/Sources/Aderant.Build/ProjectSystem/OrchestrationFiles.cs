using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem {
    internal class OrchestrationFiles {
        /// <summary>
        /// Gets or sets the project file to execute targets before entering a directory
        /// </summary>
        public string BeforeProjectFile { get; set; }

        /// <summary>
        /// Gets or sets the project file to execute targets before exiting a directory
        /// </summary>
        public string AfterProjectFile { get; set; }
        public string GroupExecutionFile { get; set; }
        public string BuildPlan { get; set; }
        public string CommonProjectFile { get; set; }

        /// <summary>
        /// Gets or sets the extensibility demands such as things we should always build
        /// </summary>
        public ExtensibilityImposition ExtensibilityImposition { get; set; }

        public IReadOnlyCollection<string> MakeFiles { get; set; }
    }
}
