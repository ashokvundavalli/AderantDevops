using System.Collections.Generic;
using System.ServiceModel;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.PipelineService {

    [ServiceContract(SessionMode = SessionMode.Allowed)]
    //[KnownAssembly(typeof(BuildOperationContext))]
    internal interface IBuildPipelineService : IVsoCommandService {
        [OperationContract]
        void Publish(BuildOperationContext context);

        [OperationContract]
        BuildOperationContext GetContext();

        [OperationContract]
        void RecordProjectOutputs(OutputFilesSnapshot snapshot);

        [OperationContract]
        void RecordArtifacts(string key, IEnumerable<ArtifactManifest> manifests);

        [OperationContract]
        void PutVariable(string scope, string variableName, string value);

        [OperationContract]
        string GetVariable(string scope, string variableName);
    }

    [ServiceContract]
    internal interface IVsoCommandService {

        [OperationContract]
        void AssociateArtifact(IEnumerable<BuildArtifact> artifacts);

        [OperationContract]
        BuildArtifact[] GetAssociatedArtifacts();
    }

}

