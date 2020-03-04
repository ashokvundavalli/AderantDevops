using System.Runtime.Serialization;
using Aderant.Build.Annotations;
using ProtoBuf;

namespace Aderant.Build {

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public sealed class BuildMetadata {

        public BuildMetadata() {
            BuildId = 0;
            ScmBranch = "";
            ScmCommitId = "";
            Flavor = "";
        }

        [DataMember]
        public string BuildNumber { get; set; }

        [DataMember]
        public int BuildId { get; set; }

        [DataMember]
        public string BuildUri { get; set; }

        [DataMember]
        public string ScmBranch { get; set; }

        [DataMember]
        public string ScmCommitId { get; set; }

        [DataMember]
        public string ScmUri { get; set; }

        [DataMember]
        public string SourceCommit { get; set; }

        [DataMember]
        public PullRequestInfo PullRequest { get; set; }

        /// <summary>
        /// Indicates if this build is running within the context of a pull request
        /// </summary>
        [ProtoIgnore]
        public bool IsPullRequest {
            get { return PullRequest != null; }
        }

        [DataMember]
        public string Flavor { get; set; }

        /// <summary>
        /// Is the build system itself in debug mode.
        /// </summary>
        [DataMember]
        public bool DebugLoggingEnabled { get; set; }

        [DataMember]
        public string BuildSourcesDirectory { get; set; }

        [UsedImplicitly]
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

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public sealed class PullRequestInfo {

        [DataMember(Name = nameof(Id))]
        public string Id { get; set; }

        [DataMember(Name = nameof(TargetBranch))]
        public string TargetBranch { get; set; }

        [DataMember(Name = nameof(SourceBranch))]
        public string SourceBranch { get; set; }
    }
}