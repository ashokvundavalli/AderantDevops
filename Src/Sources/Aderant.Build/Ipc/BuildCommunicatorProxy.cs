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

        public void PutVariable(string id, string key, string value) {
            Channel.PutVariable(id, key, value);
        }

        public string GetVariable(string id, string key) {
            return Channel.GetVariable(key, key);
        }
    }
}
