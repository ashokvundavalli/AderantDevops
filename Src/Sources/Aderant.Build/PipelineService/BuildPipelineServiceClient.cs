using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using Aderant.Build.Packaging;

namespace Aderant.Build.PipelineService {
    /// <summary>
    /// Proxy for <see cref="IBuildPipelineService" />.
    /// </summary>
    internal class BuildPipelineServiceClient : IBuildPipelineService {

        [ThreadStatic]
        private static IBuildPipelineService threadSpecificProxy;

        private static string lastSeenConnectionUri;

        private readonly string dataCollectionServiceUri;

        private object initializationLock = new object();

        private bool initialized;

        internal BuildPipelineServiceClient(string dataCollectionServiceUri) {
            this.dataCollectionServiceUri = dataCollectionServiceUri;
        }

        internal BuildPipelineServiceProxy Proxy { get; set; }

        public static IBuildPipelineService Current {
            get {
                var tlsProxy = threadSpecificProxy;
                if (tlsProxy == null) {
                    return threadSpecificProxy = CreateFromPipeId(BuildPipelineServiceHost.PipeId);
                }

                return tlsProxy;
            }
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            InvokeServiceAction(() => Proxy.AssociateArtifacts(artifacts));
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return InvokeServiceAction(() => Proxy.GetAssociatedArtifacts());
        }

        public void Publish(BuildOperationContext context) {
            InvokeServiceAction(() => Proxy.Publish(context));
        }

        public BuildOperationContext GetContext() {
            return InvokeServiceAction(() => Proxy.GetContext());
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            InvokeServiceAction(() => Proxy.RecordProjectOutputs(snapshot));
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return InvokeServiceAction(() => Proxy.GetProjectOutputs(container));
        }

        public IEnumerable<ProjectOutputSnapshot> GetAllProjectOutputs() {
            return InvokeServiceAction(() => Proxy.GetAllProjectOutputs());
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            InvokeServiceAction(() => Proxy.RecordArtifacts(container, manifests));
        }

        public void PutVariable(string scope, string variableName, string value) {
            InvokeServiceAction(() => Proxy.PutVariable(scope, variableName, value));
        }

        public string GetVariable(string scope, string variableName) {
            return InvokeServiceAction(() => Proxy.GetVariable(scope, variableName));
        }

        public void TrackProject(Guid projectGuid, string solutionRoot, string fullPath, string outputPath) {
            InvokeServiceAction(() => Proxy.TrackProject(projectGuid, solutionRoot, fullPath, outputPath));
        }

        public IEnumerable<TrackedProject> GetTrackedProjects() {
            return InvokeServiceAction(() => Proxy.GetTrackedProjects());
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            return InvokeServiceAction(() => Proxy.GetArtifactsForContainer(container));
        }

        public object[] Ping() {
            return InvokeServiceAction(() => Proxy.Ping());
        }

        public void Dispose() {
            var proxy = Proxy;

            if (proxy != null) {
                IDisposable disposable = proxy;
                disposable.Dispose();
            }

            threadSpecificProxy = null;
        }

        /// <summary>
        /// Convenience factory method for creating a client from a partial named pipe address
        /// </summary>
        /// <param name="pipeId"></param>
        internal static IBuildPipelineService CreateFromPipeId(string pipeId) {
            return new BuildPipelineServiceClient(BuildPipelineServiceHost.CreateServerUri(pipeId, "0").AbsoluteUri);
        }

        private T InvokeServiceAction<T>(Func<T> action) {
            EnsureInitialized();

            if (Proxy != null && Proxy.State == CommunicationState.Opened) {
                return action();
            }

            throw new CommunicationException("Proxy is communication state is invalid");
        }

        private void InvokeServiceAction(Action action) {
            EnsureInitialized();

            if (Proxy != null && Proxy.State == CommunicationState.Opened) {
                action();
                return;
            }

            throw new CommunicationException("Proxy is communication state is invalid");
        }

        private void EnsureInitialized() {
            if (initialized) {
                return;
            }

            object syncInitialization = initializationLock;

            lock (syncInitialization) {
                if (!initialized) {
                    InitializeInternal();
                    initialized = true;
                }
            }
        }

        private void InitializeInternal() {
            Binding binding = BuildPipelineServiceHost.CreateServerBinding();
            EndpointAddress address = new EndpointAddress(new Uri(dataCollectionServiceUri));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            BuildPipelineServiceProxy proxy;
            Exception ex;
            bool connectionValid;

            const long timeout = 60000;
            do {
                connectionValid = TestConnection(binding, address, out proxy, out ex);
                if (ex is EndpointNotFoundException) {
                    break;
                }

                Thread.Sleep(50);
            } while (stopwatch.ElapsedMilliseconds < timeout && !connectionValid);

            if (!connectionValid) {
                throw new BuildPlatformException(string.Format("Could not connect to data service within the available time {0}. Reason:{1}", timeout, ex.Message), ex);
            }

            Proxy = proxy;
        }

        private bool TestConnection(Binding binding, EndpointAddress address, out BuildPipelineServiceProxy proxy, out Exception exception) {
            proxy = null;
            exception = null;

            try {
                proxy = new BuildPipelineServiceProxy(binding, address);

                if (string.Equals(lastSeenConnectionUri, dataCollectionServiceUri)) {
                    proxy.Open();
                    return true;
                }

                proxy.Ping();

                lastSeenConnectionUri = dataCollectionServiceUri;

                return true;
            } catch (Exception ex) {
                exception = ex;
                return false;
            }
        }
    }
}
