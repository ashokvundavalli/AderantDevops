using System.Runtime.Serialization;
using Aderant.Build.DependencyAnalyzer;
using ProtoBuf;

namespace Aderant.Build {

    /// <summary>
    /// Represents a directory entry that contributes to the build.
    /// </summary>
    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal class BuildDirectoryContribution {

        private BuildDirectoryContribution() {
        }

        public BuildDirectoryContribution(string file) {
            File = file;
        }

        public string File { get; }

        /// <summary>
        /// The dependency file from the contributor
        /// </summary>
        public string DependencyFile { get; set; }

        [IgnoreDataMember]
        [ProtoIgnore]
        public DependencyManifest DependencyManifest { get; set; }
    }
}