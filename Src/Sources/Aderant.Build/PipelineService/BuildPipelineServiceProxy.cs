using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Aderant.Build.Packaging;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    internal class BuildPipelineServiceProxy : ClientBase<IBuildPipelineService> {

        static BuildPipelineServiceProxy() {
            CacheSetting = CacheSetting.AlwaysOn;
        }

        public BuildPipelineServiceProxy(Binding binding, EndpointAddress address)
            : base(binding, address) {
            Endpoint.Behaviors.Add(new ProtoEndpointBehavior());
        }

        public void Publish(BuildOperationContext context) {
            DoOperationWithFaultHandling(() => { Channel.Publish(context); });
        }

        public BuildOperationContext GetContext() {
            return DoOperationWithFaultHandling(() => Channel.GetContext());
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            DoOperationWithFaultHandling(() => { Channel.RecordProjectOutputs(snapshot); });
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return DoOperationWithFaultHandling(() => Channel.GetProjectOutputs(container));
        }

        public IEnumerable<ProjectOutputSnapshot> GetAllProjectOutputs() {
            return DoOperationWithFaultHandling(() => Channel.GetAllProjectOutputs());
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            ErrorUtilities.IsNotNull(container, nameof(container));
            DoOperationWithFaultHandling(() => { Channel.RecordArtifacts(container, manifests); });
        }

        public void PutVariable(string scope, string variableName, string value) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            DoOperationWithFaultHandling(() => { Channel.PutVariable(scope, variableName, value); });
        }

        public string GetVariable(string scope, string variableName) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            return DoOperationWithFaultHandling(() => Channel.GetVariable(scope, variableName));
        }

        public void TrackProject(Guid projectGuid, string solutionRoot, string fullPath, string outputPath) {
            ErrorUtilities.IsNotNull(fullPath, nameof(fullPath));
            DoOperationWithFaultHandling(() => { Channel.TrackProject(projectGuid, solutionRoot, fullPath, outputPath); });
        }

        public IEnumerable<TrackedProject> GetTrackedProjects() {
            return DoOperationWithFaultHandling(() => Channel.GetTrackedProjects());
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            ErrorUtilities.IsNotNull(container, nameof(container));
            return DoOperationWithFaultHandling(() => Channel.GetArtifactsForContainer(container));
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            ErrorUtilities.IsNotNull(artifacts, nameof(artifacts));
            DoOperationWithFaultHandling(() => { Channel.AssociateArtifacts(artifacts); });
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return DoOperationWithFaultHandling(() => Channel.GetAssociatedArtifacts());

        }

        private T DoOperationWithFaultHandling<T>(Func<T> func) {
            try {
                return func();
            } catch (FaultException ex) {
                throw ExceptionConverter.ConvertException(ex);
            }
        }

        private void DoOperationWithFaultHandling(Action func) {
            try {
                func();
            } catch (FaultException ex) {
                throw ExceptionConverter.ConvertException(ex);
            }
        }

        public object[] Ping() {
            return DoOperationWithFaultHandling(() => Channel.Ping());
        }

        public void SetStatus(string status, string reason) {
            DoOperationWithFaultHandling(() => Channel.SetStatus(status, reason));
        }
    }

    internal static class ExceptionConverter {

        public static Exception ConvertException(FaultException faultEx) {
            if (faultEx.Code == null || faultEx.Code.Name == null) {
                return new BuildPlatformException(faultEx.Message, faultEx);
            }

            return ConvertException(faultEx.Code.Name, faultEx.Message, faultEx);
        }

        private static Exception ConvertException(string exceptionType, string message, Exception innerException) {
            try {
                string typeName2 = "System." + exceptionType;
                return (Exception)Activator.CreateInstance(
                    typeof(Exception),
                    typeName2,
                    true,
                    BindingFlags.Default,
                    null,
                    new object[] {
                        message,
                        innerException
                    },
                    CultureInfo.InvariantCulture);
            } catch (Exception) {
                return new BuildPlatformException(message, innerException);
            }
        }
    }

    [Serializable]
    public class BuildPlatformException : Exception {
        public BuildPlatformException(string message)
            : base(message) {
        }

        public BuildPlatformException(string message, Exception innerException)
            : base(message, innerException) {
        }
    }

}
