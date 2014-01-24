using System.Collections.Generic;

namespace DependencyAnalyzer {
    public sealed class Build {
        /// <summary>
        /// Gets or sets the sequencing ordinal for this group of modules.
        /// </summary>
        /// <value>
        /// The order.
        /// </value>
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the modules contained within the group. These modules do not depend on each other.
        /// </summary>
        /// <value>
        /// The modules.
        /// </value>
        public IEnumerable<ExpertModule> Modules { get; set; }
    }
}