using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.VersionControl;

namespace Aderant.Build.ProjectSystem.StateTracking {
    [Serializable]
    [DataContract]
    public sealed class BuildStateFile : StateFileBase {

        [DataMember(EmitDefaultValue = false)]
        public BucketId BucketId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DataMember(EmitDefaultValue = false)]
        public string BuildId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string TreeSha { get; set; }

        [IgnoreDataMember]
        internal string DropLocation { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public PullRequestInfo PullRequestInfo { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ScmBranch { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ScmCommitId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        internal IDictionary<string, ProjectOutputs> Outputs { get; set; }

        [DataMember(EmitDefaultValue = false)]
        internal IDictionary<string, ICollection<ArtifactManifest>> Artifacts { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentBuildId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentTreeSha { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid ParentId { get; set; }
    }
}
