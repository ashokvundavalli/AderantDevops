using System;
using System.Collections.Concurrent;
using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GitVersion : Microsoft.Build.Utilities.Task {
        private static readonly ConcurrentDictionary<string, GitInfo> gitInfoCache = new ConcurrentDictionary<string, GitInfo>(StringComparer.OrdinalIgnoreCase);
        private GitInfo results;

        [Required]
        public string WorkingDirectory { get; set; }

        public bool Discover { get; set; }

        public bool AllowTfsBuildVariableFallback { get; set; } = true;

        /// <summary>
        /// The branch being built
        /// </summary>
        /// <value>The git branch.</value>
        [Output]
        public string FriendlyBranchName {
            get { return results.FriendlyBranchName; }
        }

        [Output]
        public string CanonicalBranchName {
            get { return results.CanonicalBranchName; }
        }

        [Output]
        public string Sha {
            get { return results.Sha; }
        }

        /// <summary>
        /// The short (7 chars) commit sha being built
        /// </summary>
        /// <value>The git commit.</value>
        [Output]
        public string Commit {
            get { return Sha.Substring(0, 7); }
        }

        public override bool Execute() {
            if (!gitInfoCache.TryGetValue(WorkingDirectory, out results)) {
                RunTask(out var friendlyBranchName, out var canonicalBranchName, out var sha);

                results = new GitInfo(AllowTfsBuildVariableFallback, friendlyBranchName, canonicalBranchName, sha);
                gitInfoCache.TryAdd(WorkingDirectory, results);
            }

            return !Log.HasLoggedErrors;
        }

        private void RunTask(out string friendlyBranchName, out string canonicalBranchName, out string sha) {
            sha = null;

            if (Discover) {
                string discover = Repository.Discover(WorkingDirectory);

                WorkingDirectory = discover;
            }

            using (var repo = new Repository(WorkingDirectory)) {
                friendlyBranchName = repo.Head.FriendlyName;
                Log.LogMessage(MessageImportance.Low, "Set FriendlyBranchName: " + friendlyBranchName);

                canonicalBranchName = repo.Head.CanonicalName;
                Log.LogMessage(MessageImportance.Low, "Set CanonicalBranchName: " + canonicalBranchName);

                try {
                    sha = repo.Head.Tip.Id.Sha;

                    Log.LogMessage(MessageImportance.Low, "Set Sha: " + sha);
                } catch {
                }
            }
        }
    }

    internal sealed class GitInfo {
        private readonly bool allowTfsBuildVariableFallback;

        public GitInfo(bool allowTfsBuildVariableFallback, string friendlyBranchName, string canonicalBranchName, string sha) {
            this.allowTfsBuildVariableFallback = allowTfsBuildVariableFallback;
            FriendlyBranchName = friendlyBranchName;
            CanonicalBranchName = canonicalBranchName;
            Sha = sha;

            // Server builds checkout a specific commit putting the repository into a DETACHED HEAD state.
            // Rather than try and find all refs that are reachable from the commit we will fall back to the TF VC
            // environment variable provided information
            if (string.IsNullOrEmpty(friendlyBranchName) || IsDetachedHead(friendlyBranchName)) {
                FriendlyBranchName = GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");
            }

            if (string.IsNullOrEmpty(canonicalBranchName) || IsDetachedHead(canonicalBranchName)) {
                CanonicalBranchName = GetEnvironmentVariable("BUILD_SOURCEBRANCH");
            }

            if (string.IsNullOrEmpty(sha)) {
                Sha = GetEnvironmentVariable("BUILD_SOURCEVERSION");
            }
        }

        public string FriendlyBranchName { get; private set; }
        public string CanonicalBranchName { get; private set; }
        public string Sha { get; set; }

        private static bool IsDetachedHead(string branchName) {
            return string.Equals(branchName, "(no branch)");
        }

        private string GetEnvironmentVariable(string variable) {
            if (allowTfsBuildVariableFallback) {
                return Environment.GetEnvironmentVariable(variable);
            }

            return null;
        }
    }
}
