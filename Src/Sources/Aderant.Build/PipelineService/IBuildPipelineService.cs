using System;
using System.Collections.Generic;
using System.ServiceModel;
using Aderant.Build.Model;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Evaluation;

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
        void TrackProject(TrackedProject trackedProject);

        [OperationContract]
        IEnumerable<TrackedProject> GetTrackedProjects();

        [OperationContract]
        IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container);

        [OperationContract]
        object[] Ping();

        [OperationContract]
        void SetStatus(string status, string reason);
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
