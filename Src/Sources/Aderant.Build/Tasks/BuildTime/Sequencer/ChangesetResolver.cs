using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace Aderant.Build.Tasks.BuildTime.Sequencer {
    public class ChangesetResolver {
        private readonly Context context;
        private string canonicalBranchName;
        private string friendlyBranchName;

        public string WorkingDirectory { get; set; }

        public bool Discover { get; set; }

        public List<Commit> Commits { get; set; }

        public List<string> ChangedFiles { get; set; }

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
            private set => friendlyBranchName = value;
        }

        public string CanonicalBranchName {
            get {
                if (string.IsNullOrEmpty(canonicalBranchName) || IsDetachdHead(canonicalBranchName)) {
                    return Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");
                }
                return canonicalBranchName;
            }
            private set => canonicalBranchName = value;
        }

        public ChangesetResolver(Context context, string workingDirectory, bool discover = true) {
            this.context = context;
            InitializeFromWorkingDirectory(workingDirectory, discover);
        }

        public void InitializeFromWorkingDirectory(string workingDirectory, bool discover) {
            WorkingDirectory = workingDirectory;
            Discover = discover;
            if (Discover) {
                string discoveredDirectory = Repository.Discover(WorkingDirectory);

                WorkingDirectory = discoveredDirectory;
            }

            if (!Directory.Exists(WorkingDirectory)) {
                throw new DirectoryNotFoundException($"Can not find path: {WorkingDirectory}");
            }

            if (context.ComboBuildType != ComboBuildType.Changed || context.ComboBuildType != ComboBuildType.Staged) {
                return;
            }

            using (Repository repo = new Repository(WorkingDirectory)) {
                FriendlyBranchName = repo.Head.FriendlyName;
                CanonicalBranchName = repo.Head.CanonicalName;

                try {
                    CommitFilter filter = new CommitFilter {
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

                    TreeChanges changes = repo.Diff.Compare<TreeChanges>(Commits[0].Tree, Commits[divergedDistance - 1].Parents.FirstOrDefault()?.Tree);
                    List<string> changedFiles = new List<string>();
                    foreach (var treeChange in changes) {
                        changedFiles.Add(treeChange.Path);
                    }

                    if (context.ComboBuildType == ComboBuildType.Changed) {
                        UpdateChangedFilesFromStatus(repo, changedFiles);
                    }
                    
                    ChangedFiles = changedFiles.Distinct().ToList();
                } catch {
                    // Ignored
                }
            }
        }

        private void UpdateChangedFilesFromStatus(Repository repo, List<string> changedFiles) {
            RepositoryStatus repositoryStatus = repo.RetrieveStatus(new StatusOptions());

            // Modified
            foreach (StatusEntry item in repositoryStatus.Modified) {
                changedFiles.Add(item.FilePath);
            }

            // Added
            foreach (StatusEntry item in repositoryStatus.Added) {
                changedFiles.Add(item.FilePath);
            }

            // Staged
            foreach (StatusEntry item in repositoryStatus.Staged) {
                changedFiles.Add(item.FilePath);
            }

            // Untracked
            foreach (StatusEntry item in repositoryStatus.Untracked) {
                changedFiles.Add(item.FilePath);
            }
        }

        private bool IsDetachdHead(string branchName) {
            return string.Equals(branchName, "(no branch)");
        }
    }
}
