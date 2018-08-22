using System.Collections.Generic;
using System.ServiceModel;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.Ipc {

    [ServiceContract(SessionMode = SessionMode.Allowed)]
    [ServiceKnownType(typeof(BuildStateFile))]
    [ServiceKnownType(typeof(List<BuildStateFile>))]
    [ServiceKnownType(typeof(BucketId))]
    [ServiceKnownType(typeof(List<BucketId>))]
    [ServiceKnownType(typeof(SourceChange))]
    [ServiceKnownType(typeof(List<SourceChange>))]
    internal interface IContextService {
        [OperationContract]
        void Publish(BuildOperationContext context);

        [OperationContract]
        BuildOperationContext GetContext();

        [OperationContract]
        void RecordProjectOutputs(OutputFilesSnapshot snapshot);

        [OperationContract]
        void RecordArtifacts(string key, ICollection<ArtifactManifest> manifests);

        [OperationContract]
        void PutVariable(string scope, string variableName, string value);

        [OperationContract]
        string GetVariable(string scope, string variableName);
    }
}
