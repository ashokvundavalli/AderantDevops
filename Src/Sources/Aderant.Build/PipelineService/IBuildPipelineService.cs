using System;
using System.Collections.Generic;
using System.ServiceModel;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.PipelineService {

    [ServiceContract]
    internal interface IBuildPipelineService : IArtifactService, IInputFileTrackingService, IProjectTrackingService, IBuildTreeContributorService, IDisposable {

        /// <summary>
        /// Ensures the service is reachable.
        /// </summary>
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

        /// <summary>
        /// Places a variable into the build property bag.
        /// </summary>
        [OperationContract]
        void PutVariable(string scope, string variableName, string value);

        /// <summary>
        /// Interrogates the build property bag for variable in the provided scope.
        /// </summary>
        [OperationContract]
        string GetVariable(string scope, string variableName);

        [OperationContract]
        void SetStatus(string status, string reason);


    }

    [ServiceContract]
    internal interface IProjectTrackingService {
        /// <summary>
        /// Makes the build aware of a project and stores a minimal set of information about the project.
        /// The data this stores can be useful in later phases of the pipeline where you need to interrogate a project but don't
        /// want to pay the performance cost of loading the project file again.
        /// </summary>
        [OperationContract]
        void TrackProject(OnDiskProjectInfo onDiskProject);

        [OperationContract]
        IEnumerable<OnDiskProjectInfo> GetTrackedProjects();

        [OperationContract(Name = "GetTrackedProjects2")]
        IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids);
    }

    [ServiceContract]
    internal interface IBuildTreeContributorService {

        [OperationContract]
        void AddBuildDirectoryContributor(BuildDirectoryContribution buildDirectoryContribution);

        [OperationContract]
        IReadOnlyCollection<BuildDirectoryContribution> GetContributors();
    }

    [ServiceContract]
    internal interface IInputFileTrackingService {

        /// <summary>
        /// Notifies the service it should track a set of files.
        /// </summary>
        [OperationContract]
        void TrackInputFileDependencies(string solutionRoot, IReadOnlyCollection<TrackedInputFile> fileDependencies);

        /// <summary>
        /// Retrieves a collection of tracked files from the service. If the service has any files for the given key
        /// they are removed from the internal buffer.
        /// </summary>
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