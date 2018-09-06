using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Aderant.Build.VersionControl.Model;
using LibGit2Sharp;
using ProtoBuf;
using FileStatus = Aderant.Build.VersionControl.Model.FileStatus;

namespace Aderant.Build.VersionControl {

    [Export(typeof(IVersionControlService))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class GitVersionControl : IVersionControlService {

        public GitVersionControl() {
        }

        /// <summary>
        /// Gets the changed files between two branches as well as the artifact bucket cache key
        /// </summary>
        public SourceTreeMetadata GetMetadata(string repositoryPath, string fromBranch, string toBranch, bool includeLocalChanges = false) {
            var info = new SourceTreeMetadata();

            HashSet<BucketId> bucketKeys = new HashSet<BucketId>();

            using (var repository = OpenRepository(repositoryPath)) {
                var workingDirectory = repository.Info.WorkingDirectory;

                // The current tree - what you have
                // It could be behind some branch you are diffing or it could be the
                // result of a pull request merge making it the new tree to be committed 
                Commit newCommit = null;

                if (string.IsNullOrWhiteSpace(fromBranch)) {
                    newCommit = repository.Head.Tip;
                    fromBranch = repository.Head.CanonicalName;
                } else {
                    var branch = repository.Branches[fromBranch];
                    if (branch != null) {
                        newCommit = branch.Tip;
                    }
                }

                if (newCommit == null) {
                    // Could not find the branch - get our current tip as a reasonable default
                    newCommit = repository.Head.Tip;
                }

                Commit oldCommit;
                if (string.IsNullOrWhiteSpace(toBranch)) {
                    // Lookup the branch history and find the joint commit and its branch.
                    // TODO: change the name of commonAncestor into something like commonBranch? The oldCommit is actually the common forking point.
                    string commonAncestor;
                    oldCommit = FindMostLikelyReusableBucket(fromBranch, repository, newCommit, out commonAncestor);
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
                        info.CommonCommit = oldCommit.Id.Sha;

                        // Collect all changed files between the two commits.
                        GetChanges(includeLocalChanges, repository, oldCommit, newCommit, workingDirectory, info);

                        if (!string.Equals(newCommit.Tree.Sha, oldCommit.Tree.Sha)) {
                            bucketKeys.Add(new BucketId(oldCommit.Tree.Sha, BucketId.Previous));
                        }

                        info.NewCommitDescription = $"{newCommit.Id.Sha}: {newCommit.MessageShort}";
                        info.OldCommitDescription = $"{oldCommit.Id.Sha}: {oldCommit.MessageShort}";
                    }

                    info.BucketIds = bucketKeys;
                }
            }

            return info;
        }

        private void GetChanges(bool includeLocalChanges, Repository repository, Commit oldCommit, Commit newCommit, string workingDirectory, SourceTreeMetadata info) {
            var treeChanges = repository.Diff.Compare<TreeChanges>(oldCommit.Tree, newCommit.Tree);
            var changes = treeChanges.Select(x => new SourceChange(workingDirectory, x.Path, (FileStatus)x.Status)).ToList();

            if (includeLocalChanges) {
                var status = repository.RetrieveStatus();
                if (status.IsDirty) {
                    AddLocalChanges(workingDirectory, changes, status);
                }
            }

            info.Changes = changes;
        }

        private void AddLocalChanges(string workingDirectory, List<SourceChange> changes, RepositoryStatus status) {
            var localChanges = status.Added.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Added))
                .Concat(status.RenamedInWorkDir.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Renamed)))
                .Concat(status.Modified.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Modified)))
                .Concat(status.Removed.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Deleted)))
                .Concat(status.Untracked.Select(s => new SourceChange(workingDirectory, s.FilePath, FileStatus.Untracked)));

            changes.AddRange(localChanges);
        }

        /// <summary>
        /// Recursively look for the parent of the current commit until reaching to a point that the commit is also reachable from
        /// another interested branch, which means probably we have the cached build of that already.
        /// </summary>
        /// <returns>
        /// The joint point where the commit is also reachable from somewhere else. The first branch name is returned in
        /// the out parameter branchCanonicalName.
        /// </returns>
        private Commit FindMostLikelyReusableBucket(string fromBranch, Repository repository, Commit currentTree, out string commonBranch) {
            Commit commit = currentTree.Parents.FirstOrDefault();

            Commit[] interestingCommit = { null };

            List<string> search = new List<string> {
                "refs/remotes/origin/master",
                "refs/heads/master",
            };

            if (!string.IsNullOrWhiteSpace(fromBranch)) {
                if (!fromBranch.StartsWith(BranchName.RefsHeads)) {
                    fromBranch = BranchName.RefsHeads + fromBranch;
                }

                var branch = CreateBranchFromRef(fromBranch, repository);
                if (branch != null) {
                    //TODO: I wonder this is not necessary as the current branch should be excluded from the interest search.
                    //search.Insert(0, branch.CanonicalName);
                }
            }

            // Recursively walk through the parents.
            while (commit != null) {
                interestingCommit[0] = commit;

                // Get the reachable branches to this commit.
                IEnumerable<Reference> reachableFrom = repository.Refs.ReachableFrom(repository.Refs, interestingCommit);
                var list = reachableFrom.Select(s => s.CanonicalName).ToList();

                // Check if there is joint item in the two lists.
                var commonJoint = list.Intersect(search).FirstOrDefault();
                if (commonJoint != null) {
                    // If found, we can return from this point.
                    commonBranch = commonJoint;
                    return commit;
                }

                // Else, go to its parent.
                commit = commit.Parents.FirstOrDefault();
            }

            commonBranch = null;
            return null;
        }

        private static Commit GetTip(string refName, Repository repository) {
            var branch = repository.Branches[refName];
            if (branch == null) {
                branch = CreateBranchFromRef(refName, repository);
            }

            if (branch != null) {
                return branch.Tip;
            }

            throw new InvalidOperationException("Unable to get branch from ref:" + refName);
        }

        private static Branch CreateBranchFromRef(string refName, Repository repository) {
            // VSTS workspaces may not have any refs/heads due to the way it clones sources
            // We instead need to check origin/<branch> or refs/remotes/origin/<branch>
            var branchName = BranchName.CreateFromRef(refName);
            var networkRemote = repository.Network.Remotes["origin"];

            if (networkRemote != null) {
                return repository.Branches[networkRemote.Name + "/" + branchName.Name];
            }

            return null;
        }

        private static Repository OpenRepository(string repositoryPath) {
            ErrorUtilities.IsNotNull(repositoryPath, nameof(repositoryPath));

            string gitDir = Repository.Discover(repositoryPath);

            return new Repository(gitDir);
        }
    }

    /// <summary>
    /// Represents the components to identify an object within the build cache
    /// </summary>
    [DebuggerDisplay("{Id} {Tag}")]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    [DataContract]
    public class BucketId : IEquatable<BucketId> {

        [DataMember]
        private readonly string id;

        [DataMember]
        private readonly string tag;

        internal BucketId() {
        }

        public BucketId(string id, string tag) {
            this.id = id;
            this.tag = tag;
        }

        public static string Current { get; } = nameof(Current);
        public static string ParentsParent { get; } = nameof(ParentsParent);
        public static string Previous { get; } = nameof(Previous);

        /// <summary>
        /// The object key - usually some kind of hash.
        /// </summary>
        public string Id {
            get { return id; }
        }

        /// <summary>
        /// Gives you the Id as a path segment for use on file systems.
        /// This is the first 2 characters of the SHA-1, separator and the remaining 38 characters.
        /// </summary>
        [IgnoreDataMember]
        public string DirectorySegment {
            get { return CreateDirectorySegment(Id); }
        }

        /// <summary>
        /// The friendly name of the object.
        /// </summary>
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

        public bool Equals(BucketId other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return string.Equals(id, other.id, StringComparison.OrdinalIgnoreCase) && string.Equals(tag, other.tag, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gives you the Id as a path segment for use on file systems.
        /// This is the first 2 characters of the SHA-1, separator and the remaining 38 characters.
        /// </summary>
        public static string CreateDirectorySegment(string bucketId) {
            return bucketId.Insert(2, @"\");
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj.GetType() != this.GetType()) {
                return false;
            }

            return Equals((BucketId)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(id) * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(tag);
            }
        }
    }

    /// <summary>
    /// Represents a version control service, such as Git or Team Foundation.
    /// </summary>
    public interface IVersionControlService {
    }

}
