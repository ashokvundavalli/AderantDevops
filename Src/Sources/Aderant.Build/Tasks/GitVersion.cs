using System;
using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public sealed class GitVersion : Microsoft.Build.Utilities.Task { 
        private string canonicalBranchName;
        private string friendlyBranchName;
        private string sha;

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
            get {
                // Server builds checkout a specific commit putting the repository into a DETACHED HEAD state.
                // Rather than try and find all refs that are reachable from the commit we will fall back to the TF VC
                // environment variable provided information
                if (string.IsNullOrEmpty(friendlyBranchName) || IsDetachedHead(friendlyBranchName)) {
                    return GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");
                }
                return friendlyBranchName;
            }
            private set { friendlyBranchName = value; }
        }

        [Output]
        public string CanonicalBranchName {
            get {
                if (string.IsNullOrEmpty(canonicalBranchName) || IsDetachedHead(canonicalBranchName)) {
                    return GetEnvironmentVariable("BUILD_SOURCEBRANCH");
                }
                return canonicalBranchName;
            }
            private set { canonicalBranchName = value; }
        }

        [Output]
        public string Sha {
            get {
                if (string.IsNullOrEmpty(sha)) {
                    return GetEnvironmentVariable("BUILD_SOURCEVERSION");
                }
                return sha;
            }
            private set { sha = value; }
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

            return !Log.HasLoggedErrors;
        }

        private bool IsDetachedHead(string branchName) {
            return string.Equals(branchName, "(no branch)");
        }

        private string GetEnvironmentVariable(string variable) {
            if (AllowTfsBuildVariableFallback) {
                return Environment.GetEnvironmentVariable(variable);
            }
            return null;
        }
    }
}
