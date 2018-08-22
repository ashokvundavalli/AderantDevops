using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Aderant.Build.Ipc {
    /// <summary>
    /// Proxy for Communicator class.
    /// </summary>
    internal class ContextServiceProxy : ClientBase<IContextService>, IContextService, IContextServiceContract {
        public ContextServiceProxy(Binding binding, EndpointAddress remoteAddress)
            : base(binding, remoteAddress) {
        }

        public void Publish(BuildOperationContext context) {
            Channel.Publish(context);
        }

        public BuildOperationContext GetContext() {
            return Channel.GetContext();
        }

        public void RecordProjectOutputs(OutputFilesSnapshot snapshot) {
            Channel.RecordProjectOutputs(snapshot);
        }

        public void RecordArtifacts(string key, ICollection<ArtifactManifest> manifests) {
            Channel.RecordArtifacts(key, manifests);
        }

        public void PutVariable(string scope, string variableName, string value) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            Channel.PutVariable(scope, variableName, value);
        }

        public string GetVariable(string scope, string variableName) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            return Channel.GetVariable(scope, variableName);
        }
    }
}
