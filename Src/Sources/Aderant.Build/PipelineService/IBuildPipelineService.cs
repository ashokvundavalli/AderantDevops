using System.Collections.Generic;
using System.ServiceModel;
using Aderant.Build.Packaging;

namespace Aderant.Build.PipelineService {

    [ServiceContract(SessionMode = SessionMode.Allowed)]
    internal interface IBuildPipelineService : IArtifactService {
        [OperationContract]
        void Publish(BuildOperationContext context);

        [OperationContract]
        BuildOperationContext GetContext();

        [OperationContract]
        void RecordProjectOutput(OutputFilesSnapshot snapshot);
        
        /// <summary>
        /// Returns the outputs for a specific publisher.
        /// </summary>
        [OperationContract]
        IEnumerable<OutputFilesSnapshot> GetProjectOutputs(string publisherName);
        
        /// <summary>
        /// Returns the outputs for all projects seen by the build.
        /// Keyed by project file.
        /// </summary>
        [OperationContract]
        IEnumerable<OutputFilesSnapshot> GetAllProjectOutputs();

        [OperationContract]
        void RecordArtifacts(string key, IEnumerable<ArtifactManifest> manifests);

        [OperationContract]
        void PutVariable(string scope, string variableName, string value);

        [OperationContract]
        string GetVariable(string scope, string variableName);
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
