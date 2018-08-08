using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Tasks;
using LibGit2Sharp;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// To detect changed file sets for the git repository.
    /// Note: the library LibGit2Sharp.dll requires the lib folder next to it containing various versions of the native library to run such as "git2-baa87df.dll".
    /// </summary>
    public class ChangesetResolver {
        private readonly BuildOperationContext context;
        private string canonicalBranchName;
        private string friendlyBranchName;

        public string WorkingDirectory { get; set; }

        public bool Discover { get; set; }

        public List<Commit> Commits { get; set; }

        public string FriendlyBranchName {
            get {
                // Server builds checkout a specific commit putting the repository into a DETACHED HEAD state.
                // Rather than try and find all refs that are reachable from the commit we will fall back to the TF VC
                // environment variabled provided information
                if (string.IsNullOrEmpty(friendlyBranchName) || IsDetachdHead(friendlyBranchName)) {
                    return Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");
                }

                return friendlyBranchName;
            }
            private set { friendlyBranchName = value; }
        }

        public string CanonicalBranchName {
            get {
                if (string.IsNullOrEmpty(canonicalBranchName) || IsDetachdHead(canonicalBranchName)) {
                    return Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");
                }
                return canonicalBranchName;
            }
            private set { canonicalBranchName = value; }
        }

        public ChangesetResolver(BuildOperationContext context, string workingDirectory, bool discover = true) {
            this.context = context;
            FindGitRootDirectory(workingDirectory, discover);
        }

        /// <summary>
        /// Point WorkingDirectory to the right directory.
        /// In case of monorepo, the root may be the up level of the current module.
        /// </summary>
        /// <param name="workingDirectory">The passed in working directory.</param>
        /// <param name="discover">Execute a discovery or not.</param>
        private void FindGitRootDirectory(string workingDirectory, bool discover) {
            WorkingDirectory = workingDirectory;
            Discover = discover;
            if (Discover) {
                string gitDir = Repository.Discover(WorkingDirectory);
                WorkingDirectory = gitDir;
            }
            if (!Directory.Exists(WorkingDirectory)) {
                throw new DirectoryNotFoundException($"Can not find path: {WorkingDirectory}");
            }
        }

        /// <summary>
        /// Get the changed files for a Pull Request. This is only used for the server build where the TFS creates a new branch to merge the PR into master. The current branch status is "detached" so it doesn't have a branch name.
        /// It is equivlant to the command: git diff --name-status HEAD..origin/master
        /// Ref: https://github.com/libgit2/libgit2sharp/wiki/Git-diff
        /// </summary>
        /// <returns>A list of strings containing the full path of the changed files. Example:
        ///   Framework\...\ConditionalExecutionTests.cs
        ///   Framework\...\ConstructorOverrideTest.cs
        /// </returns>
        public List<string> GetDiffToMaster(ChangesToConsider buildType = ChangesToConsider.PendingChanges) {
            using (var repo = new Repository(WorkingDirectory)) {
                var currentTip = repo.Head.Tip.Tree;
                var masterTip = repo.Branches["origin/master"].Tip.Tree;
                var diffs = repo.Diff.Compare<Patch>(currentTip, masterTip);
                var committedChangs = diffs.Select(x => x.Path).Distinct().ToList();
                return committedChangs;
            }
        }

        /// <summary>
        /// Get changed files from the forking point from the master, including unattached changes.
        /// </summary>
        /// <param name="buildType"></param>
        /// <returns></returns>
        public List<string> GetDiffAll(ChangesToConsider buildType = ChangesToConsider.PendingChanges) {

            //if (buildType != ChangesToConsider.PendingChanges || buildType != ChangesToConsider.Staged) {
            //    return;
            //}

            using (var repo = new Repository(WorkingDirectory)) {
                var head = repo.Head;
                FriendlyBranchName = head.FriendlyName;
                CanonicalBranchName = head.CanonicalName;

                try {
                    CommitFilter filter = new CommitFilter {
                        ExcludeReachableFrom = repo.Branches["master"],
                        IncludeReachableFrom = repo.Branches[FriendlyBranchName]
                    };

                    Commits = repo.Commits.QueryBy(filter).ToList();
                    int divergedDistance = Commits.Count;

                    foreach (Branch repoBranch in repo.Branches) {
                        if (repoBranch.FriendlyName == FriendlyBranchName) {
                            continue;
                        }

                        IEnumerable<Commit> branchCommits = repoBranch.Commits.Take(divergedDistance);
                        Commit firstMatch = Commits.FirstOrDefault(a => a == branchCommits.FirstOrDefault(b => b == a));

                        if (firstMatch != null) {
                            var tempIndex = Commits.FindIndex(a => a == firstMatch);
                            Commits.RemoveRange(tempIndex, divergedDistance - tempIndex);
                            divergedDistance = tempIndex;
                        }
                    }

                    TreeChanges changes = repo.Diff.Compare<TreeChanges>(Commits[0].Tree, Commits[divergedDistance - 1].Parents.FirstOrDefault()?.Tree);
                    List<string> changedFiles = new List<string>();
                    foreach (var treeChange in changes) {
                        changedFiles.Add(treeChange.Path);
                    }

                    if (buildType == ChangesToConsider.PendingChanges) {
                        UpdateChangedFilesFromStatus(repo, changedFiles);
                    }

                    var result = changedFiles.Distinct().ToList();
                    return result;
                } catch {
                    return null;
                }
            }
        }

        private void UpdateChangedFilesFromStatus(Repository repo, List<string> changedFiles) {
            RepositoryStatus repositoryStatus = repo.RetrieveStatus(new StatusOptions());

            //Modified
            foreach (var item in repositoryStatus.Modified) {
                changedFiles.Add(item.FilePath);
            }

            //Added
            foreach (var item in repositoryStatus.Added) {
                changedFiles.Add(item.FilePath);
            }

            //Staged
            foreach (var item in repositoryStatus.Staged) {
                changedFiles.Add(item.FilePath);
            }

            //Untracked
            foreach (var item in repositoryStatus.Untracked) {
                changedFiles.Add(item.FilePath);
            }
        }

        private bool IsDetachdHead(string branchName) {
            return string.Equals(branchName, "(no branch)");
        }
    }

}
