using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
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

        public IReadOnlyCollection<IPendingChange> GetPendingChanges(string repositoryPath) {
            string gitDir = Repository.Discover(repositoryPath);

            using (var repo = new Repository(gitDir)) {
                var status = repo.RetrieveStatus();
                
                if (!status.IsDirty) {
                    return new IPendingChange[0];
                }

                var workingDirectory = repo.Info.WorkingDirectory;

                var changes =
                    status.Added.Select(s => new PendingChange(s.FilePath, FileStatus.Added))
                        .Concat(status.RenamedInWorkDir.Select(s => new PendingChange(CleanPath(workingDirectory, s), FileStatus.Renamed)))
                        .Concat(status.Modified.Select(s => new PendingChange(CleanPath(workingDirectory, s), FileStatus.Modified)))
                        .Concat(status.Removed.Select(s => new PendingChange(CleanPath(workingDirectory, s), FileStatus.Removed)))
                        .Concat(status.Untracked.Select(s => new PendingChange(CleanPath(workingDirectory, s), FileStatus.Untracked)));

                return changes.ToList();
            }
        }

        private static string CleanPath(string workingDirectory, StatusEntry s) {
            return Path.GetFullPath(Path.Combine(workingDirectory, s.FilePath));
        }
    }

    [DebuggerDisplay("Path: {Path} Status: {Status}")]
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

        IReadOnlyCollection<IPendingChange> GetPendingChanges(string repositoryPath);
    }
}
