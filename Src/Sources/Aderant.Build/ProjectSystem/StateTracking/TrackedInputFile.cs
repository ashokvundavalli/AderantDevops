using System.IO;
using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem.StateTracking {
    [DataContract]
    [ProtoContract]
    public class TrackedInputFile {
        private TrackedInputFile() {
        }

        public TrackedInputFile(string itemSpec) {
            this.FileName = Path.GetFileName(itemSpec);
            this.FullPath = itemSpec;
        }

        public string FullPath { get; set; }

        [DataMember]
        [ProtoMember(1)]
        public string FileName { get; set; }

        [DataMember]
        [ProtoMember(2)]
        public string Sha1 { get; set; }
    }
}