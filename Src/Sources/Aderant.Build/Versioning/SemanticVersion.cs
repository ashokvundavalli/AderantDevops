using System;
using System.Globalization;

namespace Aderant.Build.Versioning {
    public class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion> {
        private readonly Version sourceVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="SemanticVersion"/> class.
        /// </summary>
        /// <param name="version">The version.</param>
        public SemanticVersion(Version version) {
            int major = version.Major;
            int minor = version.Minor;
            int build = version.Build;
            int revision = version.Revision;

            this.sourceVersion = new Version(GetVersionPart(major), GetVersionPart(minor), GetVersionPart(build), GetVersionPart(revision));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SemanticVersion"/> class.
        /// </summary>
        /// <param name="major">The major.</param>
        /// <param name="minor">The minor.</param>
        /// <param name="build">The build.</param>
        public SemanticVersion(int major, int minor, int build) : this(new Version(major, minor, build, 0)) {
        }

        private static int GetVersionPart(int part) {
            return part > 0 ? part : 0;
        }

        public int CompareTo(SemanticVersion other) {
            // Default implementation until we need pre-release support
            return this.sourceVersion.CompareTo(other.sourceVersion);
        }

        public override string ToString() {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", sourceVersion.Major, sourceVersion.Minor, sourceVersion.Build);
        }

        public bool Equals(SemanticVersion other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return sourceVersion.Equals(other.sourceVersion);
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

            return Equals((SemanticVersion) obj);
        }

        public override int GetHashCode() {
            return sourceVersion.GetHashCode();
        }
    }
}