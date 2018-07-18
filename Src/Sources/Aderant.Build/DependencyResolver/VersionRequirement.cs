using System;
using System.Linq;

namespace Aderant.Build.DependencyResolver {
    internal class ConstraintExpression {
        public static char[] Operators { get; } = new char[] {
            '~',
            '=',
            '>',
            '<',
            '-' // Supports exact match 1.0.0-ci-20170201-223443
        };

        public static string Parse(string versionValue) {
            if (!string.IsNullOrWhiteSpace(versionValue) && versionValue.IndexOfAny(Operators) < 0) {
                return "= " + versionValue;
            }

            return versionValue;
        }
    }

    internal class VersionRequirement : IEquatable<VersionRequirement> {
        private string constraintExpression;

        public string ConstraintExpression {
            get { return constraintExpression; }
            set {
                constraintExpression = value.Trim();

                if (!string.IsNullOrWhiteSpace(constraintExpression) && constraintExpression.IndexOfAny(DependencyResolver.ConstraintExpression.Operators) == -1) {
                    if (constraintExpression.Any(char.IsDigit)) {
                        // Paket can't deal with '=' characters because reasons.
                        constraintExpression = DependencyResolver.ConstraintExpression.Parse(constraintExpression);
                    } else {
                        if (!string.IsNullOrEmpty(OriginatingFile)) {
                            throw new InvalidOperationException($"The file: '{OriginatingFile}' contains an invalid expression '{constraintExpression}'. The expression must contain an operator.");
                        }

                        throw new InvalidOperationException($"Invalid expression '{constraintExpression}'. The expression must contain an operator.");
                    }
                }
            }
        }

        public string AssemblyVersion { get; set; }
        public string OriginatingFile { get; set; }

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
