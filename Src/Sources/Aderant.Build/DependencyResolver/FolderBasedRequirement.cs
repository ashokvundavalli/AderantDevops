using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver {
    internal class FolderBasedRequirement : DependencyRequirement {
        public FolderBasedRequirement(ExpertModule reference) {
            Name = reference.Name;
            Branch = reference.Branch;

            VersionRequirement = new VersionRequirement {
                AssemblyVersion = reference.AssemblyVersion
            };

            Source = reference.RepositoryType;
        }

        /// <summary>
        /// Gets or sets the branch to retrieve from. Can be null.
        /// </summary>
        /// <value>The branch.</value>
        public string Branch { get; protected set; }
    }
}