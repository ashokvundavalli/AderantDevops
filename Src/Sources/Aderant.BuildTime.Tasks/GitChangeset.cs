using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Aderant.BuildTime.Tasks {
    public sealed class GitChangeset : Microsoft.Build.Utilities.Task {
        private string canonicalBranchName;
        private string friendlyBranchName;

        [Required]
        public string WorkingDirectory { get; set; }

        public bool Discover { get; set; }

        public ITaskItem GitFolder { get; set; }

        public List<Commit> Commits { get; set; }

        public List<string> ChangedFiles { get; set; }

        /// <summary>
        /// The branch being built
        /// </summary>
        /// <value>The git branch.</value>
        [Output]
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

        [Output]
        public string CanonicalBranchName {
            get {
                if (string.IsNullOrEmpty(canonicalBranchName) || IsDetachdHead(canonicalBranchName)) {
                    return Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");
                }
                return canonicalBranchName;
            }
            private set { canonicalBranchName = value; }
        }

        public override bool Execute() {
            if (Discover) {
                string discover = Repository.Discover(WorkingDirectory);

                WorkingDirectory = discover;
            }

            using (var repo = new Repository(WorkingDirectory)) {
                FriendlyBranchName = repo.Head.FriendlyName;
                CanonicalBranchName = repo.Head.CanonicalName;

                try {
                    var filter = new CommitFilter {
                        ExcludeReachableFrom = repo.Branches["master"],
                        IncludeReachableFrom = repo.Branches[FriendlyBranchName]
                    };

                    Commits = repo.Commits.QueryBy(filter).ToList();
                    int divergedDistance = Commits.Count();

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

                    TreeChanges changes = repo.Diff.Compare<TreeChanges>(Commits[0].Tree, Commits[divergedDistance-1].Parents.FirstOrDefault().Tree);
                    List<string> changedFiles = new List<string>();
                    foreach (var treeChange in changes) {
                        changedFiles.Add(treeChange.Path);
                    }

                    ChangedFiles = changedFiles;

                } catch {
                }
            }

            return !Log.HasLoggedErrors;
        }

        private bool IsDetachdHead(string branchName) {
            return string.Equals(branchName, "(no branch)");
        }
    }
}