using System;
using System.Diagnostics;
using System.IO;

namespace Aderant.Build.Packaging {

    [DebuggerDisplay("{Location} => {Destination}")]
    [Serializable]
    public struct PathSpec : IEquatable<PathSpec> {

        public PathSpec(string location, string destination) : this(location, destination, null) {
        }

        public PathSpec(string location, string destination, bool? useHardLinks) {
            if (string.IsNullOrWhiteSpace(location)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(location));
            }

            this.Location = location;
            this.Destination = destination;
            this.UseHardLink = useHardLinks;
        }

        /// <summary>
        /// Gets the destination. Where the file should go to.
        /// </summary>
        /// <value>The destination.</value>
        public string Destination { get; }

        /// <summary>
        /// Gets the location, or source. The location where the file currently is.
        /// </summary>
        /// <value>The location.</value>
        public string Location { get; }

        /// <summary>
        /// An override to determine whether or not a hardlink is used.
        /// </summary>
        /// <value>Override on whether to use hard link.</value>
        public bool? UseHardLink { get; set; }

        public bool Equals(PathSpec other) {
            return EqualsInternal(other);
        }

        public override bool Equals(object obj) {
            if (!(obj is PathSpec)) {
                return false;
            }

            var spec = (PathSpec)obj;
            return EqualsInternal(spec);
        }

        private bool EqualsInternal(PathSpec other) {
            return string.Equals(Location, other.Location, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(Destination, other.Destination, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            var hashCode = -79747215;
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Location);
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Destination);
            return hashCode;
        }

        public static bool operator ==(PathSpec a, PathSpec b) {
            return a.EqualsInternal(b);
        }

        public static bool operator !=(PathSpec a, PathSpec b) {
            return !(a == b);
        }

        /// <summary>
        /// Creates a new path spec, taking into account Robocopy trailing slash bugs.
        /// </summary>
        public static PathSpec Create(string source, string destination) {
            return new PathSpec(
                source.TrimEnd(Path.DirectorySeparatorChar),
                destination.TrimEnd(Path.DirectorySeparatorChar));
        }
    }
}
