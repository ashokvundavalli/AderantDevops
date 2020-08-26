using System.Collections.Generic;

namespace Aderant.Build.DependencyResolver.Model {
    /// <summary>
    /// Represents a group of dependencies (typically within paket.dependencies)
    /// </summary>
    internal class DependencyGroup {
        public DependencyGroup(string groupName, Dictionary<string, VersionRequirement> name) {
            GroupName = groupName;
            Requirements = name;
        }

        /// <summary>
        /// The name of the group.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Do not resolve transitive dependencies
        /// </summary>
        public bool Strict { get; set; }

        /// <summary>
        /// Any .NET framework restrictions.
        /// </summary>
        public IEnumerable<string> FrameworkRestrictions { get; set; }

        public IDictionary<string, VersionRequirement> Requirements { get; private set; }

        /// <summary>
        /// Non-package content (http links etc)
        /// </summary>
        public IList<RemoteFile> RemoteFiles { get; set; }
    }
}