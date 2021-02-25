using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.PipelineService {
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    [DataContract]
    public class QueryOptions {
        [DataMember(EmitDefaultValue =  false)]
        public bool? IncludeStateFiles { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public bool? IncludeSourceTreeMetadata { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public bool? IncludeBuildMetadata { get; set; }
    }
}
