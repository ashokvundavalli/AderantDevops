using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.VersionControl;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem.StateTracking {
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    [DataContract]
    public sealed class BuildStateFile : StateFileBase {

        [DataMember]
        private IDictionary<string, ICollection<ArtifactManifest>> artifacts;

        [DataMember]
        private IDictionary<string, ProjectOutputSnapshot> outputs;

        [ProtoIgnore]
        [IgnoreDataMember]
        private bool requiresSerializationFixUp;

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

        internal IDictionary<string, ProjectOutputSnapshot> Outputs {
            get {
                if (requiresSerializationFixUp) {
                    outputs = new ProjectTreeOutputSnapshot(outputs);
                    requiresSerializationFixUp = false;
                }

                return outputs;
            }
            set { outputs = value; }
        }

        internal IDictionary<string, ICollection<ArtifactManifest>> Artifacts {
            get { return artifacts; }
            set { artifacts = value; }
        }

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

        internal void PrepareForSerialization() {
            // Dont serialize for the build cache with specific machine path info
            foreach (var projectOutputSnapshot in Outputs) {
                projectOutputSnapshot.Value.AbsoluteProjectFile = null;
            }

            Location = null;
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context) {
            // Protocol buffers will set state to persistence, JSON serializer does not
            if (context.State == StreamingContextStates.Persistence) {
                requiresSerializationFixUp = true;
            }
        }

        [OnSerializing]
        internal void OnSerializing(StreamingContext context) {
            if (context.State != StreamingContextStates.Persistence) {
                PrepareForSerialization();
            }
        }
    }
}
