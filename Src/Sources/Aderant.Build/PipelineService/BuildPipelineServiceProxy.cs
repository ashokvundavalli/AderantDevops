using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Aderant.Build.Packaging;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    /// <summary>
    /// Proxy for <see cref="IBuildPipelineService"/>.
    /// </summary>
    internal class BuildPipelineServiceProxy : ClientBase<IBuildPipelineService>, IBuildPipelineService, IBuildPipelineServiceContract {

        static BuildPipelineServiceProxy() {
            ClientBase<IBuildPipelineService>.CacheSetting = CacheSetting.AlwaysOn;
        }

        public BuildPipelineServiceProxy(Binding binding, EndpointAddress remoteAddress)
            : base(binding, remoteAddress) {
            Endpoint.Behaviors.Add(new ProtoEndpointBehavior());
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

        public void RecordArtifacts(string key, IEnumerable<ArtifactManifest> manifests) {
            ErrorUtilities.IsNotNull(key, nameof(key));
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

        public void AssociateArtifact(IEnumerable<BuildArtifact> artifacts) {
            ErrorUtilities.IsNotNull(artifacts, nameof(artifacts));
            Channel.AssociateArtifact(artifacts);
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return Channel.GetAssociatedArtifacts();
        }
    }
}
