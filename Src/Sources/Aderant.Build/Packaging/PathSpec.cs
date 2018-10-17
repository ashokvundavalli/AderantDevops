using System;
using System.Diagnostics;
using System.IO;

namespace Aderant.Build.Packaging {

    [DebuggerDisplay("{Location} => {Destination}")]
    public struct PathSpec {

        public PathSpec(string location, string destination) {
            this.Location = location;
            this.Destination = destination;
        }

        public string Destination { get; }

        public string Location { get; }

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
