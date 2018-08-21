using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.ProjectSystem {
    [Serializable]
    [DataContract]
    public class BuildStateMetadata {

        [DataMember]
        public IReadOnlyCollection<BuildStateFile> BuildStateFiles { get; set; }
    }
}
