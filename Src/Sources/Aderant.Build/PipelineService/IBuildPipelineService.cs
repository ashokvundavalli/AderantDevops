using System;
using System.Collections.Generic;
using System.ServiceModel;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.PipelineService {

    [ServiceContract]
    internal interface IBuildPipelineService : IArtifactService, IInputFileTrackingService, IDisposable {

        [OperationContract]
        object[] Ping();

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
        /// </summary>
        [OperationContract]
        IEnumerable<ProjectOutputSnapshot> GetProjectSnapshots();

        [OperationContract]
        void PutVariable(string scope, string variableName, string value);

        [OperationContract]
        string GetVariable(string scope, string variableName);

        [OperationContract]
        void TrackProject(OnDiskProjectInfo onDiskProject);

        [OperationContract]
        IEnumerable<OnDiskProjectInfo> GetTrackedProjects();

        [OperationContract(Name = "GetTrackedProjects2")]
        IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids);

        [OperationContract]
        void SetStatus(string status, string reason);
    }

    [ServiceContract]
    internal interface IInputFileTrackingService {

        [OperationContract]
        void TrackInputFileDependencies(string solutionRoot, IReadOnlyCollection<TrackedInputFile> fileDependencies);

        [OperationContract]
        IReadOnlyCollection<TrackedInputFile> ClaimTrackedInputFiles(string tag);
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

        [OperationContract]
        IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container);

        [OperationContract]
        void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests);
    }
}