using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Aderant.Build.VersionControl.Model;
using LibGit2Sharp;
using FileStatus = Aderant.Build.VersionControl.Model.FileStatus;

namespace Aderant.Build.VersionControl {
    [Export(typeof(IVersionControlService))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class GitVersionControlService : IVersionControlService {

        public GitVersionControlService() {
        }

        /// <summary>
        /// Gets the changed files between two branches as well as the artifact bucket cache key
        /// </summary>
        public SourceTreeMetadata GetMetadata(string repositoryPath, string fromBranch, string toBranch, bool includeLocalChanges = false) {
            var info = new SourceTreeMetadata();

            HashSet<BucketId> bucketKeys = new HashSet<BucketId>();

            if (string.IsNullOrWhiteSpace(repositoryPath)) {
                info.BucketIds = bucketKeys;
                return info;
            }

            using (var repository = OpenRepository(repositoryPath)) {
                var workingDirectory = repository.Info.WorkingDirectory;

                // The current tree - what you have
                // It could be behind some branch you are diffing or it could be the
                // result of a pull request merge making it the new tree to be committed
                Commit newCommit = null;

                if (string.IsNullOrWhiteSpace(fromBranch)) {
                    newCommit = repository.Head.Tip;
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
        private Commit FindMostLikelyReusableBucket(Repository repository, Commit currentTree, out string commonBranch) {
            Commit commit = currentTree.Parents.FirstOrDefault();

            Commit[] interestingCommit = { null };

            List<string> search = new List<string> {
                "refs/remotes/origin/master",
                "refs/heads/master"
            };

            // Recursively walk through the parents.
            while (commit != null) {
                interestingCommit[0] = commit;

                // Get the reachable branches to this commit.
                IEnumerable<Reference> reachableFrom = repository.Refs.ReachableFrom(repository.Refs, interestingCommit);
                var list = reachableFrom.Select(s => s.CanonicalName).ToList();

                // Check if there is joint item in the two lists.
                var commonJoint = list.Intersect(search, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
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
}
