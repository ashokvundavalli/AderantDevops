using System;

namespace Aderant.Build.Packaging {
    internal struct PathSpec {

        public PathSpec(string location, string destinationRelativePath) {
            this.Location = location;
            this.Destination = destinationRelativePath;
        }

        public string Destination { get; }

        public string Location { get; }
        public static PathSpec BuildSucceeded { get; } = new PathSpec("build.succeeded", null);

        public override bool Equals(object obj) {
            if (!(obj is PathSpec)) {
                return false;
            }

            var spec = (PathSpec)obj;
            return Location == spec.Location && Destination == spec.Destination;
        }

        public override int GetHashCode() {
            var hashCode = -79747215;
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Location);
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Destination);
            return hashCode;
        }
    }
}
