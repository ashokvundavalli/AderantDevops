using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Aderant.Build.Services;
using LibGit2Sharp;

namespace Aderant.Build.VersionControl {

    [Export(typeof(IVersionControlService))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class GitVersionControl : IVersionControlService {

        public GitVersionControl() {
        }

        public void Initialize(Context context) {
        }

        public IEnumerable<IPendingChange> GetPendingChanges(string workingDirectory) {
            string gitDir = Repository.Discover(workingDirectory);

            using (var repo = new Repository(gitDir)) {
                var status = repo.RetrieveStatus();

                if (!status.IsDirty) {
                    return Enumerable.Empty<IPendingChange>();
                }

                IEnumerable<PendingChange> changes =
                    status.Added.Select(s => new PendingChange(s.FilePath, FileStatus.Added))
                        .Concat(status.RenamedInWorkDir.Select(s => new PendingChange(s.FilePath, FileStatus.Renamed)))
                        .Concat(status.Modified.Select(s => new PendingChange(s.FilePath, FileStatus.Modified)))
                        .Concat(status.Removed.Select(s => new PendingChange(s.FilePath, FileStatus.Removed)))
                        .Concat(status.Untracked.Select(s => new PendingChange(s.FilePath, FileStatus.Untracked)));

                return changes;
            }
        }
    }

    internal class PendingChange : IPendingChange {
        public PendingChange(string path, FileStatus status) {
            Path = path;
            Status = status;
        }

        public string Path { get; }
        public FileStatus Status { get; }
    }

    public interface IPendingChange {
        string Path { get; }
        FileStatus Status { get; }
    }

    public enum FileStatus {
        Added,
        Removed,
        Renamed,
        Deleted,
        Untracked,
        Modified
    }

    /// <summary>
    /// Represents a version control service, such as Git or Team Foundation.
    /// </summary>
    public interface IVersionControlService : IFlexService {

        IEnumerable<IPendingChange> GetPendingChanges(string workingDirectory);
    }
}
