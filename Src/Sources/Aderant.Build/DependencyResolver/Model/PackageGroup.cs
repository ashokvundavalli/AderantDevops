using System.Collections.Generic;
using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.DependencyResolver.Model {
    [DataContract]
    [ProtoContract]
    public class PackageGroup {

        [DataMember]
        [ProtoMember(1)]
        public string Name { get; set; }

        [DataMember]
        [ProtoMember(2)]
        private ICollection<PackageInfo> packageInfo;

        public ICollection<PackageInfo> PackageInfo {
            get => packageInfo;
            set => packageInfo = value;
        }

        private PackageGroup() {
        }

        public PackageGroup(string name, ICollection<PackageInfo> packageInfo) {
            this.Name = name;
            this.PackageInfo = packageInfo;
        }

        [OnDeserializing]
        internal void OnDeserializing(StreamingContext context) {
            PackageInfo = new List<PackageInfo>();
        }
    }
}
