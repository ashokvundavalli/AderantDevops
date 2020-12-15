using System;

namespace Aderant.Build.DependencyResolver.Model {
    /// <summary>
    /// Represents a file
    /// </summary>
    internal class RemoteFile : IDependencyRequirement, IEquatable<RemoteFile> {
        public string Uri { get; }

        /// <summary>
        /// Creates a new RemoteFile instance.
        /// </summary>
        /// <param name="itemName">The custom name of the download</param>
        /// <param name="uri">The location of the resource to download</param>
        /// <param name="groupName">The group the resource belongs to within the dependency file</param>
        public RemoteFile(string itemName, string uri, string groupName) {
            this.ItemName = itemName;

            Uri = uri;
            Group = groupName;
        }

        public string Name {
            get { return Uri; }
        }

        public string Group { get; }
        public VersionRequirement VersionRequirement { get; set; }
        public bool ReplaceVersionConstraint { get; set; }
        public bool ReplicateToDependencies { get; set; }
        public string PostProcess { get; set; }

        public string ItemName { get; }

        public bool Equals(RemoteFile other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return string.Equals(ItemName, other.ItemName, StringComparison.OrdinalIgnoreCase) && string.Equals(Uri, other.Uri, StringComparison.OrdinalIgnoreCase);
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

            return Equals((RemoteFile) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return ((ItemName != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(ItemName) : 0) * 397) ^ (Uri != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Uri) : 0);
            }
        }
    }
}
