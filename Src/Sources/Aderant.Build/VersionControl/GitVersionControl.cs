using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using LibGit2Sharp;

namespace Aderant.Build.VersionControl {

    [Export(typeof(IVersionControlService))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class GitVersionControl : IVersionControlService {

        public GitVersionControl() {
        }

        public IReadOnlyCollection<ISourceChange> GetPendingChanges(BuildMetadata buildMetadata, string repositoryPath) {
            ErrorUtilities.IsNotNull(repositoryPath, nameof(repositoryPath));

            string gitDir = Repository.Discover(repositoryPath);

            using (var repo = new Repository(gitDir)) {
                var workingDirectory = repo.Info.WorkingDirectory;

                IEnumerable<SourceChange> changes = Enumerable.Empty<SourceChange>();

                if (buildMetadata != null) {
                    if (buildMetadata.IsPullRequest) {
                        var currentTip = repo.Head.Tip.Tree;
                        Tree tip = null;

                        foreach (var branch in repo.Branches) {
                            // UpstreamBranchCanonicalName appears to hold the name in the TFS format of /refs/heads/<foo>
                            var upstreamBranchCanonicalName = branch.UpstreamBranchCanonicalName;
                            if (string.Equals(upstreamBranchCanonicalName, buildMetadata.PullRequest.TargetBranch, StringComparison.OrdinalIgnoreCase)) {
                                tip = branch.Tip.Tree;
                                break;
                            }
                        }

                        if (tip != null) {
                            var patch = repo.Diff.Compare<Patch>(currentTip, tip);
                            changes = patch.Select(x => new SourceChange(workingDirectory, x.Path, (FileStatus)x.Status)); // Mapped LibGit2Sharp.ChangeKind to our FileStatus which is in the same order.
                        }

                    }

                    if (buildMetadata.BuildId > 0) {
                        // TODO: here we need to query the last successful build for the same branch/commit and mix in the changes
                    }
                }

                var status = repo.RetrieveStatus();
                if (!status.IsDirty) {
                    return new ISourceChange[0];
                }

                // Get the current git status.
                var uncommittedChanges =
                    status.Added.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Added))
                        .Concat(status.RenamedInWorkDir.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Renamed)))
                        .Concat(status.Modified.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Modified)))
                        .Concat(status.Removed.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Deleted)))
                        .Concat(status.Untracked.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Untracked)));

                var result = changes.Concat(uncommittedChanges).ToList();
                return result;
            }
        }

        /// <summary>
        /// Gets the changed files between two branches as well as the artifact bucket cache key
        /// </summary>
        public SourceTreeMetadata GetMetadata(string repositoryPath, string fromBranch, string toBranch) {
            System.Diagnostics.Debugger.Launch();
            var info = new SourceTreeMetadata();

            List<BucketId> bucketKeys = new List<BucketId>();

            using (var repository = OpenRepository(repositoryPath)) {
                var workingDirectory = repository.Info.WorkingDirectory;

                // The current tree - what you have
                // It could be behind some branch you are diffing or it could be the
                // result of a pull request merge making it the new tree to be committed 
                Commit newCommit = null;

                if (string.IsNullOrWhiteSpace(fromBranch)) {
                    newCommit = repository.Head.Tip;
                }

                if (newCommit == null) {
                    // Could not find the branch - get our current tip as a reasonable default
                    newCommit = repository.Head.Tip;
                }

                Commit oldCommit;
                if (string.IsNullOrWhiteSpace(toBranch)) {
                    string commonAncestor;
                    oldCommit = FindMostLikelyReusableBucket(repository, newCommit, out commonAncestor);
                    info.CommonAncestor = commonAncestor;
                } else {
                    oldCommit = GetTip(toBranch, repository);
                }

                if (newCommit != null) {
                    bucketKeys.Add(new BucketId(newCommit.Tree.Sha, BucketId.Current));

                    foreach (var e in newCommit.Tree) {
                        if (e.Mode == Mode.Directory) {
                            var targetSha = e.Target.Sha;
                            bucketKeys.Add(new BucketId(targetSha, e.Name));
                        }
                    }

                    if (oldCommit != null) {
                        var treeChanges = repository.Diff.Compare<TreeChanges>(oldCommit.Tree, newCommit.Tree);
                        info.Changes = treeChanges.Select(x => new SourceChange(workingDirectory, x.Path, (FileStatus)x.Status)).ToList();

                        bucketKeys.Add(new BucketId(oldCommit.Tree.Sha, BucketId.Previous));

                        Commit parentsParent = oldCommit.Parents.FirstOrDefault();
                        if (parentsParent != null) {
                            bucketKeys.Add(new BucketId(parentsParent.Tree.Sha, BucketId.ParentsParent));
                        }

                        info.NewCommitDisplay = $"{newCommit.Id.Sha}: {newCommit.MessageShort}";
                        info.OldCommitDisplay = $"{oldCommit.Id.Sha}: {oldCommit.MessageShort}";
                    }

                    info.BucketIds = bucketKeys;
                }
            }

            return info;
        }

        private Commit FindMostLikelyReusableBucket(Repository repository, Commit currentTree, out string branchCanonicalName) {
            Commit commit = currentTree.Parents.FirstOrDefault();
            Commit[] interestingCommit = { null };

            var search = new string[] {
                "refs/heads/master",
                //"refs/heads/releases/",
                //"refs/heads/dev/",
                //"refs/heads/patch/",

                "refs/remotes/origin/master",
                //"refs/remotes/origin/releases/",
                //"refs/remotes/origin/dev/",
                //"refs/remotes/origin/patch/",
            };

            while (commit != null) {
                interestingCommit[0] = commit;

                IEnumerable<Reference> reachableFrom = repository.Refs.ReachableFrom(repository.Refs, interestingCommit);
                var list = reachableFrom.Select(s => s.CanonicalName).ToList();

                foreach (var item in list) {
                    branchCanonicalName = item;

                    if (string.Equals("refs/heads/master", item, StringComparison.OrdinalIgnoreCase)) {
                        return GetTip(item, repository);
                    }

                    foreach (var name in search) {
                        if (item.StartsWith(name, StringComparison.OrdinalIgnoreCase)) {
                            return GetTip(item, repository);
                        }
                    }
                }

                commit = commit.Parents.FirstOrDefault();
            }

            branchCanonicalName = null;
            return null;
        }

        private static Commit GetTip(string branchName, Repository repository) {
            Reference repositoryRef = repository.Refs[branchName];
            
            foreach (var branch in repository.Branches) {
                string upstreamBranchCanonicalName = string.Empty;
                if (branch.TrackedBranch != null) {
                    try {
                        upstreamBranchCanonicalName = branch.UpstreamBranchCanonicalName;
                    } catch {
                    }
                }

                var isMatch = new[] {
                    branch.FriendlyName,
                    branch.CanonicalName,
                    upstreamBranchCanonicalName
                }.Contains(branchName, StringComparer.OrdinalIgnoreCase);

                if (isMatch) {
                    return branch.Tip;
                }
            }

            return null;
        }

        private static Repository OpenRepository(string repositoryPath) {
            ErrorUtilities.IsNotNull(repositoryPath, nameof(repositoryPath));

            string gitDir = Repository.Discover(repositoryPath);

            return new Repository(gitDir);
        }
    }

    [DebuggerDisplay("{Id} {Tag}")]
    [Serializable]
    [DataContract]
    public class BucketId {
        [DataMember]
        private readonly string id;

        [DataMember]
        private readonly string tag;

        public BucketId(string id, string tag) {
            this.id = id;
            this.tag = tag;
        }

        public static string Current { get; } = nameof(Current);
        public static string ParentsParent { get; } = nameof(ParentsParent);
        public static string Previous { get; } = nameof(Previous);

        public string Id {
            get { return id; }
        }

        public string Tag {
            get { return tag; }
        }

        internal bool IsRoot {
            get {
                if (this.Tag == Current) {
                    return true;
                }

                if (this.Tag == Previous) {
                    return true;
                }

                if (this.Tag == ParentsParent) {
                    return true;
                }

                return false;
            }
        }
    }

    [DebuggerDisplay("Path: {Path} Status: {Status}")]
    [Serializable]
    public class SourceChange : ISourceChange {

        public SourceChange(string workingDirectory, string relativePath, FileStatus status) {
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

    public interface ISourceChange {
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

        IReadOnlyCollection<ISourceChange> GetPendingChanges(BuildMetadata buildMetadata, string repositoryPath);
    }
}
