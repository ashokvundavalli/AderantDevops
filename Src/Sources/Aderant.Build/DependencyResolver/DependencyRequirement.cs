using System;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver {
    internal class DependencyRequirement : IDependencyRequirement, IEquatable<DependencyRequirement> {
        private string group;

        protected DependencyRequirement() {
        }

        protected DependencyRequirement(string packageName, string groupName, VersionRequirement version) {
            Name = packageName;
            Group = groupName;
            VersionRequirement = version;
        }

        /// <summary>
        /// Gets or sets the name of the requirement.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; protected set; }

        /// <summary>
        /// Gets or sets the group name of the requirement.
        /// </summary>
        /// <value>The group name.</value>
        public string Group {
            get {
                if (string.IsNullOrWhiteSpace(group)) {
                    return BuildConstants.MainDependencyGroup;
                }
                return group;
            }
            protected set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }
                group = value;
            }
        }

        /// <summary>
        /// Gets or sets the version required.
        /// </summary>
        /// <value>The version requirement.</value>
        public VersionRequirement VersionRequirement { get; protected set; }

        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        /// <value>The source.</value>
        public GetAction Source { get; protected set; }

        public static IDependencyRequirement Create(ExpertModule reference) {
            if (reference.GetAction == GetAction.NuGet) {
                return new DependencyRequirement(reference.Name, BuildConstants.MainDependencyGroup, reference.VersionRequirement);
            }
            return new FolderBasedRequirement(reference);
        }

        public static IDependencyRequirement Create(string id, string groupName, VersionRequirement version = null) {
            return new DependencyRequirement(id, groupName, version) {
                Source = GetAction.NuGet
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