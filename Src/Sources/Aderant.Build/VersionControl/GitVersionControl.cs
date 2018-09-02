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

            List<BucketId> bucketKeys = new List<BucketId>();
            
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

        private Commit FindMostLikelyReusableBucket(string fromBranch, Repository repository, Commit currentTree, out string branchCanonicalName) {
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
                    search.Insert(0, branch.CanonicalName);
                }
            }

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
    public class BucketId {

        internal BucketId() {

        }

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

        /// <summary>
        /// The object key - usually some kind of hash.
        /// </summary>
        public string Id {
            get { return id; }
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
    }

    /// <summary>
    /// Represents a version control service, such as Git or Team Foundation.
    /// </summary>
    public interface IVersionControlService {

    }

}
