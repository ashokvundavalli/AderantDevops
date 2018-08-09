﻿using System;
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
            ErrorUtilities.IsNotNull(repositoryPath, nameof(repositoryPath));

            string gitDir = Repository.Discover(repositoryPath);

            using (var repo = new Repository(gitDir)) {
                var workingDirectory = repo.Info.WorkingDirectory;

                IEnumerable<PendingChange> changes = Enumerable.Empty<PendingChange>();

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
                            changes = patch.Select(x => new PendingChange(workingDirectory, x.Path, (FileStatus)x.Status)); // Mapped LibGit2Sharp.ChangeKind to our FileStatus which is in the same order.
                        }

                    }

                    if (buildMetadata.BuildId > 0) {
                        // TODO: here we need to query the last successful build for the same branch/commit and mix in the changes
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
                Commit newTree;

                if (string.IsNullOrWhiteSpace(fromBranch)) {
                    newTree = repository.Head.Tip;
                } else {
                    newTree = GetTip(fromBranch, repository);
                }

                Commit oldTree;
                if (string.IsNullOrWhiteSpace(toBranch)) {
                    string commonAncestor;
                    oldTree = FindMostLikelyReusableBucket(repository, newTree, out commonAncestor);
                    info.CommonAncestor = commonAncestor;
                } else {
                    oldTree = GetTip(toBranch, repository);
                }

                if (newTree != null && oldTree != null) {

                    var treeChanges = repository.Diff.Compare<TreeChanges>(oldTree.Tree, newTree.Tree);
                    info.Changes = treeChanges.Select(x => new PendingChange(workingDirectory, x.Path, (FileStatus)x.Status)).ToList();

                    bucketKeys.Add(new BucketId(newTree.Sha, BucketId.Current));
                    bucketKeys.Add(new BucketId(oldTree.Sha, BucketId.Previous));

                    Commit commit = oldTree.Parents.FirstOrDefault();
                    if (commit != null) {
                        bucketKeys.Add(new BucketId(commit.Tree.Sha, BucketId.ParentsParent));
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
                        return commit;
                    }

                    foreach (var name in search) {
                        if (item.StartsWith(name, StringComparison.OrdinalIgnoreCase)) {
                            return commit;
                        }
                    }
                }

                commit = commit.Parents.FirstOrDefault();
            }

            branchCanonicalName = null;
            return null;
        }

        private static Commit GetTip(string branchName, Repository repository) {
            if (!branchName.StartsWith("refs/heads/")) {
                branchName = "refs/heads/" + branchName;
            }

            foreach (var branch in repository.Branches) {
                var isMatch = new[] {
                    branch.FriendlyName,
                    branch.UpstreamBranchCanonicalName,
                    branch.CanonicalName,
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
    public class BucketId {

        public BucketId(string id) {
            this.Id = id;
        }

        public BucketId(string id, string tag) {
            this.Id = id;
            this.Tag = tag;
        }

        public static string Current { get; } = nameof(Current);
        public static string ParentsParent { get; } = nameof(ParentsParent);
        public static string Previous { get; } = nameof(Previous);

        public string Id { get; }

        public string Tag { get; }
    }

    [DebuggerDisplay("Path: {Path} Status: {Status}")]
    [Serializable]
    public class PendingChange : IPendingChange {

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
