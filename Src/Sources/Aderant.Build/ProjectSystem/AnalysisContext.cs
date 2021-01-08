using System;
using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem {
    internal class AnalysisContext {

        public IReadOnlyCollection<string> ProjectFiles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Specifies the WIX targets path. WIX may not be installed globally and this will be the branch provided target path.
        /// </summary>
        public string WixTargetsPath { get; set; }
    }
}
