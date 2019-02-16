using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.PipelineService {
    /// <summary>
    /// Technology agnostic proxy for <see cref="IBuildPipelineService" />.
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

        /// <summary>
        /// The wrapped data service proxy
        /// </summary>
        internal BuildPipelineServiceProxy InnerProxy { get; set; }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.AssociateArtifacts(artifacts));
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetAssociatedArtifacts());
        }

        public void Publish(BuildOperationContext context) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.Publish(context));
        }

        public BuildOperationContext GetContext() {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetContext());
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.RecordProjectOutputs(snapshot));
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetProjectOutputs(container));
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectSnapshots() {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetProjectSnapshots());
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.RecordArtifacts(container, manifests));
        }

        public void PutVariable(string scope, string variableName, string value) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.PutVariable(scope, variableName, value));
        }

        public string GetVariable(string scope, string variableName) {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetVariable(scope, variableName));
        }

        public void TrackProject(OnDiskProjectInfo onDiskProject) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.TrackProject(onDiskProject));
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects() {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetTrackedProjects());
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids) {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetTrackedProjects(ids));
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetArtifactsForContainer(container));
        }

        public object[] Ping() {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.Ping());
        }

        public void SetStatus(string status, string reason) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.SetStatus(status, reason));
        }

        /// <summary>
        /// Notifies listeners of build progress.
        /// </summary>
        public void SetProgress(string currentOperation, string activity, string statusDescription) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.SetProgress(currentOperation, activity, statusDescription));
        }

        public void TrackInputFileDependencies(string solutionRoot, IReadOnlyCollection<TrackedInputFile> fileDependencies) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.TrackInputFileDependencies(solutionRoot, fileDependencies));
        }

        public IReadOnlyCollection<TrackedInputFile> ClaimTrackedInputFiles(string tag) {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.ClaimTrackedInputFiles(tag));
        }

        public void AddBuildDirectoryContributor(BuildDirectoryContribution buildDirectoryContribution) {
            InvokeServiceAction(() => InnerProxy.ChannelContract.AddBuildDirectoryContributor(buildDirectoryContribution));
        }

        public IReadOnlyCollection<BuildDirectoryContribution> GetContributors() {
            return InvokeServiceAction(() => InnerProxy.ChannelContract.GetContributors());
        }

        public void Dispose() {
            var proxy = InnerProxy;

            if (proxy != null) {
                try {
                    IDisposable disposable = proxy;
                    disposable.Dispose();
                } catch {

                }
            }

            threadSpecificProxy = null;
        }

        /// <summary>
        /// Returns a proxy to communicate with a data collection service.
        /// </summary>
        public static IBuildPipelineService GetProxy(string id) {
            return CreateFromPipeId(id);
        }

        /// <summary>
        /// Returns a proxy to communicate with the ambient data collection service for the current build environment.
        /// </summary>
        public static IBuildPipelineService GetCurrentProxy() {
            var tlsProxy = threadSpecificProxy;
            if (tlsProxy == null) {
                var proxy = GetProxy(BuildPipelineServiceHost.PipeId);
                return threadSpecificProxy = proxy;
            }

            return tlsProxy;
        }

        /// <summary>
        /// Convenience factory method for creating a client from a partial named pipe address
        /// </summary>
        /// <param name="pipeId"></param>
        private static IBuildPipelineService CreateFromPipeId(string pipeId) {
            var client = new BuildPipelineServiceClient(BuildPipelineServiceHost.CreateServerUri(pipeId, "0").AbsoluteUri);
            return client;
        }

        private T InvokeServiceAction<T>(Func<T> action) {
            EnsureInitialized();

            if (InnerProxy != null && InnerProxy.State == CommunicationState.Opened) {
                try {
                    return action();
                } catch (FaultException ex) {
                    throw ExceptionConverter.ConvertException(ex);
                }
            }

            throw new CommunicationException("Proxy is communication state is invalid");
        }

        private void InvokeServiceAction(Action action) {
            EnsureInitialized();

            if (InnerProxy != null && InnerProxy.State == CommunicationState.Opened) {
                try {
                    action();
                    return;
                } catch (FaultException ex) {
                    throw ExceptionConverter.ConvertException(ex);
                }
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
            Binding binding = BuildPipelineServiceHost.CreateNamedPipeBinding();
            EndpointAddress address = new EndpointAddress(new Uri(dataCollectionServiceUri));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            BuildPipelineServiceProxy proxy;
            Exception ex;
            bool connectionValid;

            var timeout = TimeSpan.FromSeconds(60).TotalMilliseconds;

            do {
                connectionValid = TestConnection(binding, address, out proxy, out ex);
                if (ex is EndpointNotFoundException) {
                    break;
                }

                if (!connectionValid) {
                    Thread.Sleep(50);
                }
            } while (stopwatch.ElapsedMilliseconds < timeout && !connectionValid);

            if (!connectionValid) {
                throw new BuildPlatformException(string.Format("Could not connect to data service within the available time {0}. Reason:{1}", timeout, ex.Message), ex);
            }

            InnerProxy = proxy;
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

                proxy.ChannelContract.Ping();

                lastSeenConnectionUri = dataCollectionServiceUri;

                return true;
            } catch (Exception ex) {
                exception = ex;
                return false;
            }
        }
    }

}