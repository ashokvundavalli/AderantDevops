using System;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver {
    internal class DependencyRequirement : IDependencyRequirement, IEquatable<DependencyRequirement> {
        protected DependencyRequirement() {
        }

        protected DependencyRequirement(string packageName, VersionRequirement version) {
            Name = packageName;
            VersionRequirement = version;
        }

        /// <summary>
        /// Gets or sets the name of thee requirement.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; protected set; }

        /// <summary>
        /// Gets or sets the version required.
        /// </summary>
        /// <value>The version requirement.</value>
        public VersionRequirement VersionRequirement { get; protected set; }

        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        /// <value>The source.</value>
        public RepositoryType Source { get; protected set; }

        public static IDependencyRequirement Create(ExpertModule reference) {
            if (reference.RepositoryType == RepositoryType.NuGet) {
                return new DependencyRequirement(reference.Name, reference.VersionRequirement) { ReplicateToDependencies = reference.ReplicateToDependencies };
            }
            return new FolderBasedRequirement(reference);
        }

        public static IDependencyRequirement Create(string packageName, VersionRequirement version = null) {
            return new DependencyRequirement(packageName, version) {
                Source = RepositoryType.NuGet
            };
        }

        public bool Equals(DependencyRequirement other) {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Source == other.Source && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && Equals(VersionRequirement, other.VersionRequirement);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((DependencyRequirement)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = (int)Source;
                hashCode = (hashCode * 397) ^ (Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0);
                hashCode = (hashCode * 397) ^ (VersionRequirement != null ? VersionRequirement.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(DependencyRequirement left, DependencyRequirement right) {
            return Equals(left, right);
        }

        public static bool operator !=(DependencyRequirement left, DependencyRequirement right) {
            return !Equals(left, right);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can override (blat) any contraint expression in your dependency file.
        /// </summary>
        public bool ReplaceVersionConstraint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to replicate this instance to the dependencies folder (otherwise it just stays in package)
        /// </summary>
        public bool? ReplicateToDependencies { get; set; }
    }
}