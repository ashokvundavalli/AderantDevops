using System.Collections.Generic;
using System.ServiceModel;

namespace Aderant.Build.Ipc {
    /// <summary>
    /// Contract implementation.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
    internal class BuildCommunicator : IBuildCommunicator {
        private BuildOperationContext ctx;

        public void Publish(BuildOperationContext context) {
            ctx = context;
        }

        public BuildOperationContext GetContext() {
            return ctx;
        }

        public void RecordProjectOutputs(OutputFilesSnapshot snapshot) {
            ctx.RecordProjectOutputs(snapshot);
        }

        public void RecordArtifacts(string key, ICollection<ArtifactManifest> manifests) {
            ctx.RecordArtifact(key, manifests);
        }

        public void PutVariable(string id, string key, string value) {
            ctx.PutVariable(id, key, value);
        }

        public string GetVariable(string id, string key) {
            return ctx.GetVariable(id, key);
        }
    }
}
