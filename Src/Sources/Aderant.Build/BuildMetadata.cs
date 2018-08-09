using System;
using System.Runtime.Serialization;

namespace Aderant.Build {

    [Serializable]
    public sealed class BuildMetadata {

        public BuildMetadata() {
            BuildId = 0;
            JobName = "";
            ScmBranch = "";
            ScmCommitId = "";
            Flavor = "";
        }

        public string BuildNumber { get; set; }

        public int BuildId { get; set; }

        public Uri BuildUri { get; set; }

        public string JobName { get; set; }

        public Uri JobUri { get; set; }

        public string ScmBranch { get; set; }

        public string ScmCommitId { get; set; }

        public Uri ScmUri { get; set; }

        public PullRequestInfo PullRequest { get; set; }

        /// <summary>
        /// Indicates if this build is running within the context of a pull request
        /// </summary>
        public bool IsPullRequest {
            get { return PullRequest != null; }
        }

        public string Flavor { get; set; }

        /// <summary>
        /// Is the build system itself in debug mode.
        /// </summary>
        public bool DebugLoggingEnabled { get; set; }

        public string BuildSourcesDirectory { get; set; }

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

    [Serializable]
    [DataContract]
    public sealed class PullRequestInfo {

        [DataMember(Name = "Id")]
        public string Id { get; set; }

        [DataMember(Name = "TargetBranch")]
        public string TargetBranch { get; set; }

        [DataMember(Name = "SourceBranch")]
        public string SourceBranch { get; set; }
    }
}
