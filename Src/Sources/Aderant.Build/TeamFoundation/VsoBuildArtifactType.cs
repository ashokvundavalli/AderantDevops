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
        /// Visual Studio Team Services/TFS
        /// </summary>
        [EnumMember]
        Container,

        /// <summary>
        /// A file share
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
