using Aderant.Build.DependencyAnalyzer;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public class ExtractWebModule : Microsoft.Build.Utilities.Task {
        /// <summary>
        /// Gets or sets the name of the module to extract.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        [Required]
        public string ModuleName { get; set; }

        /// <summary>
        /// Gets or sets the dependency directory.
        /// </summary>
        /// <value>
        /// The dependency directory.
        /// </value>
        [Required]
        public string DependenciesDirectory { get; set; }
          
        public override bool Execute() {
            Log.LogMessage("Replicating {0} to {1}", ModuleName, DependenciesDirectory);

            WebModule.ExtractModule(ModuleName, DependenciesDirectory);

            return !Log.HasLoggedErrors;
        }
    }
}