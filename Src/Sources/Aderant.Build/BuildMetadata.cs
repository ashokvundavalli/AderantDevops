﻿using System;

namespace Aderant.Build {

    [Serializable]
    public sealed class BuildMetadata {
   
        public BuildMetadata() {
            BuildId = "";
            JobName = "";
            ScmBranch = "";
            ScmCommitId = "";
            HostEnvironment = HostEnvironment.Developer;
        }

        public int BuildNumber { get; set; }

        public string BuildId { get; set; }

        public Uri BuildUri { get; set; }

        public string JobName { get; set; }

        public Uri JobUri { get; set; }

        public string ScmBranch { get; set; }

        public string ScmCommitId { get; set; }

        public Uri ScmUri { get; set; }

        public HostEnvironment HostEnvironment { get; set; }

        public PullRequestInfo PullRequest { get; set; }

        public bool IsPullRequest => PullRequest != null;

        /// <summary>
        /// Is the build system itself in debug mode. 
        /// </summary>
        public bool DebugLoggingEnabled { get; set; }

        public void SetPullRequestInfo(string id, string sourceBranch, string targetBranch) {
            if (id == null) {
                return;
            }

            PullRequest = new PullRequestInfo {
                Id = id,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch
            };
        }
    }

    public enum HostEnvironment {
        Developer,
        Vsts
    }

    [Serializable]
    public sealed class PullRequestInfo {
        public string Id { get; set; }
        public string TargetBranch { get; set; }
        public string SourceBranch { get; set; }
    }
}
