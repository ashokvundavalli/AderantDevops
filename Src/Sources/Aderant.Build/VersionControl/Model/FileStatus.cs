using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.VersionControl.Model {
    /// <summary>
    /// The kind of changes that a Diff can report.
    /// Copied from libgit2sharp/LibGit2Sharp/ChangeKind.cs to isolate the library.
    /// </summary>
    [DataContract]
    [ProtoContract]
    public enum FileStatus {
        /// <summary>
        /// No changes detected.
        /// </summary>
        [EnumMember]
        Unmodified = 0,

        /// <summary>
        /// The file was added.
        /// </summary>
        [EnumMember]
        Added = 1,

        /// <summary>
        /// The file was deleted.
        /// </summary>
        [EnumMember]
        Deleted = 2,

        /// <summary>
        /// The file content was modified.
        /// </summary>
        [EnumMember]
        Modified = 3,

        /// <summary>
        /// The file was renamed.
        /// </summary>
        [EnumMember]
        Renamed = 4,

        /// <summary>
        /// The file was copied.
        /// </summary>
        [EnumMember]
        Copied = 5,

        /// <summary>
        /// The file is ignored in the workdir.
        /// </summary>
        [EnumMember]
        Ignored = 6,

        /// <summary>
        /// The file is untracked in the workdir.
        /// </summary>
        [EnumMember]
        Untracked = 7,

        /// <summary>
        /// The type (i.e. regular file, symlink, submodule, ...)
        /// of the file was changed.
        /// </summary>
        [EnumMember]
        TypeChanged = 8,

        /// <summary>
        /// Entry is unreadable.
        /// </summary>
        [EnumMember]
        Unreadable = 9,

        /// <summary>
        /// Entry is currently in conflict.
        /// </summary>
        [EnumMember]
        Conflicted = 10,
    }
}
