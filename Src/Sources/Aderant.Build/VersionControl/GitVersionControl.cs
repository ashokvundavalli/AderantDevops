using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using LibGit2Sharp;

namespace Aderant.Build.VersionControl {

    [Export(typeof(IVersionControlService))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class GitVersionControl : IVersionControlService {

        public GitVersionControl() {
        }

        public void Initialize(Context context) {
        }

        public IReadOnlyCollection<IPendingChange> GetPendingChanges(string repositoryPath) {
            string gitDir = Repository.Discover(repositoryPath);

            using (var repo = new Repository(gitDir)) {
                var workingDirectory = repo.Info.WorkingDirectory;

                // Get committed changes different to master.
                var currentTip = repo.Head.Tip.Tree;
                var masterTip = repo.Branches["origin/master"].Tip.Tree;
                var diffs = repo.Diff.Compare<Patch>(currentTip, masterTip);
                var committedChangs = diffs.Select(x=>new PendingChange(workingDirectory, x.Path, FileStatus.Renamed));

                var status = repo.RetrieveStatus();
                if (!status.IsDirty) {
                    return new IPendingChange[0];
                }

                // Get the current git status.
                var uncommittedChanges =
                    status.Added.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Added))
                        .Concat(status.RenamedInWorkDir.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Renamed)))
                        .Concat(status.Modified.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Modified)))
                        .Concat(status.Removed.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Removed)))
                        .Concat(status.Untracked.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Untracked)));

                var result = committedChangs.Concat(uncommittedChanges).ToList();
                return result;
            }
        }
    }

    [DebuggerDisplay("Path: {Path} Status: {Status}")]
    internal class PendingChange : IPendingChange {

        public PendingChange(string workingDirectory, string relativePath, FileStatus status) {
            Path = relativePath;
            FullPath = CleanPath(workingDirectory, relativePath);
            Status = status;
        }

        public string FullPath { get; }

        public string Path { get; }
        public FileStatus Status { get; }

        private static string CleanPath(string workingDirectory, string relativePath) {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(workingDirectory, relativePath));
        }
    }

    public interface IPendingChange {
        string Path { get; }
        string FullPath { get; }
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
    public interface IVersionControlService {

        IReadOnlyCollection<IPendingChange> GetPendingChanges(string repositoryPath);
    }
}
