using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.DependencyResolver.Model;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem.StateTracking {
    [DataContract]
    [ProtoContract]
    public class TrackedMetadataFile : TrackedInputFile {

        [DataMember]
        [ProtoMember(3)]
        public string PackageHash { get; set; }

        [DataMember]
        [ProtoMember(4)]
        public ICollection<PackageGroup> PackageGroups { get; set; }

        [DataMember]
        [ProtoMember(5)]
        public bool TrackPackageHash { get; set; }

        private TrackedMetadataFile() {
        }

        public TrackedMetadataFile(string itemSpec) : base(itemSpec) {
        }

        public override void EnrichStateFile(BuildStateFile stateFile) {
            stateFile.PackageHash = PackageHash;
            stateFile.PackageGroups = PackageGroups;
            stateFile.TrackPackageHash = TrackPackageHash;
        }

        [OnDeserializing]
        internal void OnDeserializing(StreamingContext context) {
            PackageGroups = new List<PackageGroup>();
        }
    }
}
