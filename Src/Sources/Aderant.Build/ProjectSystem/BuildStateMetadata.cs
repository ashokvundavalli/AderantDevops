using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.ProjectSystem.StateTracking;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public class BuildStateMetadata {

        [DataMember]
        public IReadOnlyCollection<BuildStateFile> BuildStateFiles { get; set; }
    }
}
