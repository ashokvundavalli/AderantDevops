using System;
using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem {
    public class RelatedFilesManifest {

        public Dictionary<string, List<string>> RelatedFiles { get; set; }

        public RelatedFilesManifest() {
            RelatedFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
