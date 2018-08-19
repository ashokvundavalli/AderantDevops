using System;
using System.Collections.Generic;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.ProjectSystem {
    [Serializable]
    public class BuildStateMetadata {
        public IReadOnlyCollection<BuildStateFile> BuildStateFiles { get; set; }
    }
}
