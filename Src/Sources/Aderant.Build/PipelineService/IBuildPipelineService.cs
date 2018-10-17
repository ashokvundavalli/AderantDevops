using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using Aderant.Build.Packaging;
using ProtoBuf;

namespace Aderant.Build.PipelineService {

    [ServiceContract]
    internal interface IBuildPipelineService : IArtifactService, IDisposable {
        [OperationContract]
        void Publish(BuildOperationContext context);

        [OperationContract]
        BuildOperationContext GetContext();

        [OperationContract]
        void RecordProjectOutputs(ProjectOutputSnapshot snapshot);

        /// <summary>
        /// Returns the outputs for a specific container.
        /// </summary>
        [OperationContract]
        IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container);

        /// <summary>
        /// Returns the outputs for all projects seen by the build.
        /// Keyed by project file.
        /// </summary>
        [OperationContract]
        IEnumerable<ProjectOutputSnapshot> GetAllProjectOutputs();

        [OperationContract]
        void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests);

        [OperationContract]
        void PutVariable(string scope, string variableName, string value);

        [OperationContract]
        string GetVariable(string scope, string variableName);

        [OperationContract]
        void TrackProject(Guid projectGuid, string solutionRoot, string fullPath, string outputPath);

        [OperationContract]
        IEnumerable<TrackedProject> GetTrackedProjects();

        [OperationContract]
        IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container);

        [OperationContract]
        object[] Ping();

        [OperationContract]
        void SetStatus(string status, string reason);
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    internal class TrackedProject {

        [DataMember]
        public Guid ProjectGuid { get; set; }

        /// <summary>
        /// The full local path to the project file
        /// </summary>
        [DataMember]
        public string FullPath { get; set; }

        [DataMember]
        public string SolutionRoot { get; set; }

        [DataMember]
        public string OutputPath { get; set; }
    }

    [ServiceContract]
    internal interface IArtifactService {

        /// <summary>
        /// Makes an artifact known to the build.
        /// </summary>
        /// <param name="artifacts"></param>
        [OperationContract]
        void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts);

        /// <summary>
        /// Gets the artifacts known to the current build
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        BuildArtifact[] GetAssociatedArtifacts();
    }

}
