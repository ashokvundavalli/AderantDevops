using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.VersionControl {

    /// <summary>
    /// Represents the components to identify an object within the build cache
    /// </summary>
    [DebuggerDisplay("{Id} {Tag}")]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    [DataContract]
    public class BucketId : IEquatable<BucketId> {

        [DataMember]
        private readonly string id;

        [DataMember]
        private readonly string tag;

        [DataMember]
        private readonly BucketVersion version;

        internal BucketId() {
        }

        public BucketId(string id, string tag, BucketVersion version) {
            this.id = id;
            this.tag = tag;
            this.version = version;
        }

        /// <summary>
        /// The well know name that represents the version of the current source tree.
        /// </summary>
        public static string Current { get; } = nameof(Current);

        public static string ParentsParent { get; } = nameof(ParentsParent);

        /// <summary>
        /// The well know name that represents the version of the previous source tree.
        /// </summary>
        public static string Previous { get; } = nameof(Previous);

        /// <summary>
        /// The object key - the SHA1 hash.
        /// </summary>
        public string Id {
            get { return id; }
        }

        /// <summary>
        /// Gives you the Id as a path segment for use on file systems.
        /// This is the first 2 characters of the SHA-1, separator and the remaining 38 characters.
        /// </summary>
        [IgnoreDataMember]
        public string DirectorySegment {
            get { return CreateDirectorySegment(Id); }
        }

        /// <summary>
        /// The friendly name of the object.
        /// </summary>
        public string Tag {
            get { return tag; }
        }

        internal bool IsWellKnown {
            get {
                if (string.Equals(this.Tag, Current, StringComparison.Ordinal)) {
                    return true;
                }

                if (string.Equals(this.Tag, Previous, StringComparison.Ordinal)) {
                    return true;
                }

                if (string.Equals(this.Tag, ParentsParent, StringComparison.Ordinal)) {
                    return true;
                }

                return false;
            }
        }

        public BucketVersion Version {
            get {
                return version;
            }
        }

        /// <summary>
        /// Gives you the Id as a path segment for use on file systems.
        /// This is the first 2 characters of the SHA-1, separator and the remaining 38 characters.
        /// </summary>
        public static string CreateDirectorySegment(string bucketId) {
            return bucketId.Insert(2, @"\");
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj.GetType() != this.GetType()) {
                return false;
            }
            return Equals((BucketId) obj);
        }


        public bool Equals(BucketId other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return string.Equals(id, other.id, StringComparison.OrdinalIgnoreCase) && string.Equals(tag, other.tag, StringComparison.OrdinalIgnoreCase) && version == other.version;
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = (id != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(id) : 0);
                hashCode = (hashCode * 397) ^ (tag != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(tag) : 0);
                hashCode = (hashCode * 397) ^ (int) version;
                return hashCode;
            }
        }
    }

}
