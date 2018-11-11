using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyRequirement {
        /// <summary>
        /// Gets the name of the requirement (module name or package name).
        /// </summary>
        /// <value>The name.</value>
        string Name { get; }

        /// <summary>
        /// Gets the group name of the requirement (module name or package name).
        /// </summary>
        /// <value>The group name.</value>
        string Group { get; }
        
        VersionRequirement VersionRequirement { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can override (blat) any constraint expression in your dependency file.
        /// </summary>
        bool ReplaceVersionConstraint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to replicate this instance to the dependencies folder (otherwise it just stays in package)
        /// </summary>
        bool ReplicateToDependencies { get; set; }
    }
}
