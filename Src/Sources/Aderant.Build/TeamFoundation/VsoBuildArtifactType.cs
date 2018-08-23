using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.TeamFoundation {
    /// <summary>
    /// Provides the type of a TF Build artifact.
    /// </summary>
    [DataContract]
    [ProtoContract]
    public enum VsoBuildArtifactType {
        /// <summary>
        /// The container type.
        /// </summary>
        [EnumMember]
        Container,

        /// <summary>
        /// The file path type.
        /// </summary>
        [EnumMember]
        FilePath,

        /// <summary>
        /// The version control path type.
        /// </summary>
        [EnumMember]
        VersionControl,

        /// <summary>
        /// The git reference type.
        /// </summary>
        [EnumMember]
        GitRef,

        /// <summary>
        /// The TFVC label type.
        /// </summary>
        [EnumMember]
        TfvcLabel
    }
}
