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

        public IReadOnlyCollection<IPendingChange> GetPendingChanges(BuildMetadata buildMetadata, string repositoryPath) {
            string gitDir = Repository.Discover(repositoryPath);

            using (var repo = new Repository(gitDir)) {
                var workingDirectory = repo.Info.WorkingDirectory;

                IEnumerable<PendingChange> changes = Enumerable.Empty<PendingChange>();

                if (buildMetadata != null) {
                    if (buildMetadata.IsPullRequest) {
                        System.Diagnostics.Debugger.Launch();
                        var currentTip = repo.Head.Tip.Tree;
                        var masterTip = repo.Branches[buildMetadata.PullRequest.SourceBranch].Tip.Tree;
                        var patch = repo.Diff.Compare<Patch>(currentTip, masterTip);
                        changes = patch.Select(x => new PendingChange(workingDirectory, x.Path, (FileStatus)x.Status)); // Mapped LibGit2Sharp.ChangeKind to our FileStatus which is in the same order.
                    }
                }

                var status = repo.RetrieveStatus();
                if (!status.IsDirty) {
                    return new IPendingChange[0];
                }

                // Get the current git status.
                var uncommittedChanges =
                    status.Added.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Added))
                        .Concat(status.RenamedInWorkDir.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Renamed)))
                        .Concat(status.Modified.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Modified)))
                        .Concat(status.Removed.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Deleted)))
                        .Concat(status.Untracked.Select(s => new PendingChange(workingDirectory, s.FilePath, FileStatus.Untracked)));

                var result = changes.Concat(uncommittedChanges).ToList();
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

    /// <summary>
    /// The kind of changes that a Diff can report.
    /// Copied from libgit2sharp/LibGit2Sharp/ChangeKind.cs to isolate the library.
    /// </summary>
    public enum FileStatus {
        /// <summary>
        /// No changes detected.
        /// </summary>
        Unmodified = 0,

        /// <summary>
        /// The file was added.
        /// </summary>
        Added = 1,

        /// <summary>
        /// The file was deleted.
        /// </summary>
        Deleted = 2,

        /// <summary>
        /// The file content was modified.
        /// </summary>
        Modified = 3,

        /// <summary>
        /// The file was renamed.
        /// </summary>
        Renamed = 4,

        /// <summary>
        /// The file was copied.
        /// </summary>
        Copied = 5,

        /// <summary>
        /// The file is ignored in the workdir.
        /// </summary>
        Ignored = 6,

        /// <summary>
        /// The file is untracked in the workdir.
        /// </summary>
        Untracked = 7,

        /// <summary>
        /// The type (i.e. regular file, symlink, submodule, ...)
        /// of the file was changed.
        /// </summary>
        TypeChanged = 8,

        /// <summary>
        /// Entry is unreadable.
        /// </summary>
        Unreadable = 9,

        /// <summary>
        /// Entry is currently in conflict.
        /// </summary>
        Conflicted = 10,
    }

    /// <summary>
    /// Represents a version control service, such as Git or Team Foundation.
    /// </summary>
    public interface IVersionControlService {

        IReadOnlyCollection<IPendingChange> GetPendingChanges(BuildMetadata buildMetadata, string repositoryPath);
    }
}
