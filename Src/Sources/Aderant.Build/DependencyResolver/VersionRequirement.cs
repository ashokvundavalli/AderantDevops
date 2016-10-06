using System;

namespace Aderant.Build.DependencyResolver {
    internal class VersionRequirement : IEquatable<VersionRequirement> {
        public string ConstraintExpression { get; set; }
        public string AssemblyVersion { get; set; }

        public bool Equals(VersionRequirement other) {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(ConstraintExpression, other.ConstraintExpression, StringComparison.OrdinalIgnoreCase) && string.Equals(AssemblyVersion, other.AssemblyVersion, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((VersionRequirement)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return ((ConstraintExpression != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(ConstraintExpression) : 0) * 397) ^ (AssemblyVersion != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(AssemblyVersion) : 0);
            }
        }

        public static bool operator ==(VersionRequirement left, VersionRequirement right) {
            return Equals(left, right);
        }

        public static bool operator !=(VersionRequirement left, VersionRequirement right) {
            return !Equals(left, right);
        }
    }
}