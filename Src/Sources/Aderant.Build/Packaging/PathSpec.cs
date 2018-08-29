using System;
using System.Diagnostics;

namespace Aderant.Build.Packaging {

    [DebuggerDisplay("{Location} => {Destination}")]
    internal struct PathSpec {

        public PathSpec(string location, string destination) {
            this.Location = location;
            this.Destination = destination;
        }

        public string Destination { get; }

        public string Location { get; }

        public static PathSpec BuildSucceeded { get; } = new PathSpec("build.succeeded", null);

        public override bool Equals(object obj) {
            if (!(obj is PathSpec)) {
                return false;
            }

            var spec = (PathSpec)obj;
            return EqualsInternal(spec);
        }

        private bool EqualsInternal(PathSpec spec) {
            return Location == spec.Location && Destination == spec.Destination;
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
    }
}
