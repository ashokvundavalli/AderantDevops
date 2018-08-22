using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace Aderant.Build.Ipc {
    /// <summary>
    /// Contract implementation.
    /// </summary>
    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        IncludeExceptionDetailInFaults = true,
        ConcurrencyMode = ConcurrencyMode.Single,
        MaxItemsInObjectGraph = Int32.MaxValue)]
    internal class ContextService : IContextService {
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

        public void PutVariable(string scope, string variableName, string value) {
            ctx.PutVariable(scope, variableName, value);
        }

        public string GetVariable(string scope, string variableName) {
            return ctx.GetVariable(scope, variableName);
        }
    }
}
