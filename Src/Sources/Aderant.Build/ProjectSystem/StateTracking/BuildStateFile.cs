using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.DependencyResolver.Model;
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

        [DataMember]
        private IDictionary<string, string> buildConfiguration;

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

        internal IDictionary<string, string> BuildConfiguration {
            get => buildConfiguration;
            set => buildConfiguration = value;
        }

        internal IDictionary<string, ProjectOutputSnapshot> Outputs {
            get {
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

        [DataMember(EmitDefaultValue = false)]
        public ICollection<TrackedInputFile> TrackedFiles { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public ICollection<PackageGroup> PackageGroups { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string PackageHash { get; set; }

        internal void PrepareForSerialization() {
            Location = null;
        }

        [OnDeserializing]
        internal void OnDeserializing(StreamingContext context) {
            outputs = new ProjectTreeOutputSnapshot();
            artifacts = new ArtifactCollection();
            BuildConfiguration = new Dictionary<string, string>();
            PackageGroups = new List<PackageGroup>();
        }

        [OnSerializing]
        internal void OnSerializing(StreamingContext context) {
            if (context.State != StreamingContextStates.Persistence) {
                PrepareForSerialization();
            }
        }

        internal bool GetArtifacts(string containerKey, out ICollection<ArtifactManifest> artifactManifests) {
            return Artifacts.TryGetValue(containerKey, out artifactManifests);
        }

        /// <summary>
        /// Gets the projects GUIDs contained within the snapshot.
        /// </summary>
        public IReadOnlyCollection<Guid> GetProjectGuids() {
            List<Guid> ids = new List<Guid>();

            foreach (KeyValuePair<string, ProjectOutputSnapshot> snapshot in outputs) {
                ids.Add(snapshot.Value.ProjectGuid);
            }

            return ids;
        }
    }
}
