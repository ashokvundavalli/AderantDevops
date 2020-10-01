using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.DependencyResolver.Model {
    [DataContract]
    [ProtoContract]
    public class PackageInfo {

        [DataMember]
        [ProtoMember(1)]
        public string Name { get; set; }

        [DataMember]
        [ProtoMember(2)]
        public string Version { get; set; }

        private PackageInfo() {
        }

        public PackageInfo(string name, string version) {
            this.Name = name;
            this.Version = version;
        }
    }
}
