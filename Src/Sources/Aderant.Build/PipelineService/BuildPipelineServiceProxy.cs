using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Aderant.Build.Packaging;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    /// <summary>
    /// Proxy for <see cref="IBuildPipelineService" />.
    /// </summary>
    internal class BuildPipelineServiceProxy : ClientBase<IBuildPipelineService>, IBuildPipelineService, IBuildPipelineServiceContract {
        [ThreadStatic]
        private static IBuildPipelineService threadSpecificProxy;

        static BuildPipelineServiceProxy() {
            CacheSetting = CacheSetting.AlwaysOn;
        }

        internal BuildPipelineServiceProxy(Binding binding, string remoteAddress)
            : this(binding, new EndpointAddress(remoteAddress)) {
        }

        public BuildPipelineServiceProxy(Binding binding, EndpointAddress remoteAddress)
            : base(binding, remoteAddress) {
            Endpoint.Behaviors.Add(new ProtoEndpointBehavior());
        }

        public BuildPipelineServiceProxy(string contextEndpoint)
            : this(BuildPipelineServiceHost.CreateBinding(), BuildPipelineServiceHost.CreateAddress(contextEndpoint)) {
        }

        public static IBuildPipelineService Current {
            get {
                var tlsProxy = threadSpecificProxy;
                if (tlsProxy == null) {
                    return threadSpecificProxy = new BuildPipelineServiceProxy(BuildPipelineServiceHost.CreateBinding(), BuildPipelineServiceHost.CreateAddress(BuildPipelineServiceHost.PipeId));
                }

                return tlsProxy;
            }
        }

        public void Publish(BuildOperationContext context) {
            Channel.Publish(context);
        }

        public BuildOperationContext GetContext() {
            return Channel.GetContext();
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            Channel.RecordProjectOutputs(snapshot);
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return Channel.GetProjectOutputs(container);
        }

        public IEnumerable<ProjectOutputSnapshot> GetAllProjectOutputs() {
            return Channel.GetAllProjectOutputs();
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            ErrorUtilities.IsNotNull(container, nameof(container));
            Channel.RecordArtifacts(container, manifests);
        }

        public void PutVariable(string scope, string variableName, string value) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            Channel.PutVariable(scope, variableName, value);
        }

        public string GetVariable(string scope, string variableName) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            return Channel.GetVariable(scope, variableName);
        }

        public void TrackProject(Guid projectGuid, string solutionRoot, string fullPath) {
            ErrorUtilities.IsNotNull(fullPath, nameof(fullPath));
            Channel.TrackProject(projectGuid, solutionRoot, fullPath);
        }

        public IEnumerable<TrackedProject> GetTrackedProjects() {
            return Channel.GetTrackedProjects();
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            ErrorUtilities.IsNotNull(container, nameof(container));
            return Channel.GetArtifactsForContainer(container);
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            ErrorUtilities.IsNotNull(artifacts, nameof(artifacts));
            Channel.AssociateArtifacts(artifacts);
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return Channel.GetAssociatedArtifacts();
        }
    }
}
