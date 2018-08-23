using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.VersionControl;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem.StateTracking {
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
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

        [DataMember(EmitDefaultValue = false)]
        public PullRequestInfo PullRequestInfo { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ScmBranch { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ScmCommitId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        internal IDictionary<string, OutputFilesSnapshot> Outputs { get; set; }

        [DataMember(EmitDefaultValue = false)]        
        internal IDictionary<string, ICollection<ArtifactManifest>> Artifacts { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentBuildId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentTreeSha { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid ParentId { get; set; }

        /// <summary>
        /// Specifies the origin of this file.
        /// This property should not be written to the storage media and should only be set when the object is live. 
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        internal string DropLocation { get; set; }
    }
}
