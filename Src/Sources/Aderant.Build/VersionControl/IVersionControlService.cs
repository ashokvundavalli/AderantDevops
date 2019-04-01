using System.Threading;

namespace Aderant.Build.VersionControl {
    /// <summary>
    /// Represents a version control service, such as Git or Team Foundation.
    /// </summary>
    public interface IVersionControlService {

        /// <summary>
        /// Gets the changed files between two branches as well as the artifact bucket cache key
        /// </summary>
        SourceTreeMetadata GetMetadata(string repositoryPath, string fromBranch, string toBranch, bool includeLocalChanges = false, CancellationToken cancellationToken = default(CancellationToken));
    }
}