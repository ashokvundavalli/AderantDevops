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

        internal BucketId() {
        }

        public BucketId(string id, string tag) {
            this.id = id;
            this.tag = tag;
        }

        public static string Current { get; } = nameof(Current);
        public static string ParentsParent { get; } = nameof(ParentsParent);
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

        internal bool IsRoot {
            get {
                if (this.Tag == Current) {
                    return true;
                }

                if (this.Tag == Previous) {
                    return true;
                }

                if (this.Tag == ParentsParent) {
                    return true;
                }

                return false;
            }
        }

        public bool Equals(BucketId other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return string.Equals(id, other.id, StringComparison.OrdinalIgnoreCase) && string.Equals(tag, other.tag, StringComparison.OrdinalIgnoreCase);
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

            return Equals((BucketId)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(id) * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(tag);
            }
        }
    }

}
