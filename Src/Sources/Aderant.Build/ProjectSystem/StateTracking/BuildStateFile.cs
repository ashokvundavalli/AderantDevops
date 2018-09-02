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

        /// <summary>
        /// A unique identifier for this state file instance
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The build id that produced this state file.
        /// </summary>
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

        [DataMember(EmitDefaultValue = false, Name = nameof(Outputs))]
        internal IDictionary<string, OutputFilesSnapshot> Outputs { get; set; }

        [DataMember(EmitDefaultValue = false, Name = nameof(Artifacts))]        
        internal IDictionary<string, ICollection<ArtifactManifest>> Artifacts { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentBuildId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentTreeSha { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid ParentId { get; set; }

        /// <summary>
        /// Specifies the origin of this file.
        /// This property should not be serialized and should only be set when the object is being used. 
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        internal string Location { get; set; }
    }
}
