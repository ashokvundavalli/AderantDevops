using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    /// <summary>
    /// Technology agnostic proxy for <see cref="IBuildPipelineService" />.
    /// </summary>
    internal class BuildPipelineServiceClient : IBuildPipelineService {
        static BuildPipelineServiceClient() {
            ClientBase<IBuildPipelineService>.CacheSetting = CacheSetting.AlwaysOn;
        }

        private static string lastSeenConnectionUri;

        private readonly string dataCollectionServiceUri;

        private readonly object initializationLock = new object();

        internal BuildPipelineServiceClient(string dataCollectionServiceUri) {
            this.dataCollectionServiceUri = dataCollectionServiceUri;
        }

        /// <summary>
        /// The wrapped data service proxy
        /// </summary>
        internal BuildPipelineServiceProxy InnerProxy { get; set; }

        internal IBuildPipelineService Contract {
            get {
                return InnerProxy.ChannelContract;
            }
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            InvokeServiceAction(() => Contract.AssociateArtifacts(artifacts));
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return InvokeServiceAction(() => Contract.GetAssociatedArtifacts());
        }

        public void Publish(BuildOperationContext context) {
            InvokeServiceAction(() => Contract.Publish(context));
        }

        public BuildOperationContext GetContext() {
            return InvokeServiceAction(() => Contract.GetContext());
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            InvokeServiceAction(() => Contract.RecordProjectOutputs(snapshot));
        }

        public void RecordImpactedProjects(IEnumerable<string> impactedProjects) {
            InvokeServiceAction(() => Contract.RecordImpactedProjects(impactedProjects));
        }

        public void RecordRelatedFiles(Dictionary<string, List<string>> relatedFiles) {
            InvokeServiceAction(() => Contract.RecordRelatedFiles(relatedFiles));
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return InvokeServiceAction(() => Contract.GetProjectOutputs(container));
        }

        public IEnumerable<string> GetImpactedProjects() {
            return InvokeServiceAction(() => Contract.GetImpactedProjects());
        }

        public Dictionary<string, List<string>> GetRelatedFiles() {
            return InvokeServiceAction(() => Contract.GetRelatedFiles());
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectSnapshots() {
            return InvokeServiceAction(() => Contract.GetProjectSnapshots());
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            InvokeServiceAction(() => Contract.RecordArtifacts(container, manifests));
        }

        public void PutVariable(string scope, string variableName, string value) {
            InvokeServiceAction(() => Contract.PutVariable(scope, variableName, value));
        }

        public string GetVariable(string scope, string variableName) {
            return InvokeServiceAction(() => Contract.GetVariable(scope, variableName));
        }

        public void TrackProject(OnDiskProjectInfo onDiskProject) {
            InvokeServiceAction(() => Contract.TrackProject(onDiskProject));
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects() {
            return InvokeServiceAction(() => Contract.GetTrackedProjects());
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids) {
            return InvokeServiceAction(() => Contract.GetTrackedProjects(ids));
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            return InvokeServiceAction(() => Contract.GetArtifactsForContainer(container));
        }

        public object[] Ping() {
            return InvokeServiceAction(() => Contract.Ping());
        }

        public void SetStatus(string status, string reason) {
            InvokeServiceAction(() => Contract.SetStatus(status, reason));
        }

        /// <summary>
        /// Notifies listeners of build progress.
        /// </summary>
        public void SetProgress(string currentOperation, string activity, string statusDescription) {
            InvokeServiceAction(() => Contract.SetProgress(currentOperation, activity, statusDescription));
        }

        public void TrackInputFileDependencies(string solutionRoot, IReadOnlyCollection<TrackedInputFile> fileDependencies) {
            InvokeServiceAction(() => Contract.TrackInputFileDependencies(solutionRoot, fileDependencies));
        }

        public IReadOnlyCollection<TrackedInputFile> ClaimTrackedInputFiles(string tag) {
            return InvokeServiceAction(() => Contract.ClaimTrackedInputFiles(tag));
        }

        public void AddBuildDirectoryContributor(BuildDirectoryContribution buildDirectoryContribution) {
            InvokeServiceAction(() => Contract.AddBuildDirectoryContributor(buildDirectoryContribution));
        }

        public IReadOnlyCollection<BuildDirectoryContribution> GetContributors() {
            return InvokeServiceAction(() => Contract.GetContributors());
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

            //threadSpecificProxy = null;
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
            //var tlsProxy = threadSpecificProxy;
            //if (tlsProxy == null) {
                var proxy = GetProxy(BuildPipelineServiceHost.PipeId);

                return proxy;
                //    return threadSpecificProxy = proxy;
                //}

                //return tlsProxy;
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
            //if (factory != null) {
            //    return;
            //}

            object syncInitialization = initializationLock;

            lock (syncInitialization) {
                InitializeInternal();
            }
        }

        private void InitializeInternal() {
            Binding binding = BuildPipelineServiceHost.NamedPipeBinding;
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
                System.Diagnostics.Debugger.Launch();
                throw new BuildPlatformException(string.Format("Could not connect to data service within the available time {0}. Reason:{1}", timeout, ex.Message), ex);
            }

            InnerProxy = proxy;
        }

        private bool TestConnection(Binding binding, EndpointAddress address, out BuildPipelineServiceProxy proxy, out Exception exception) {
            proxy = null;
            exception = null;

            try {
                var newProxy = new BuildPipelineServiceProxy(binding, address);
                // Ref cache should only have 1 member in it if WCF caching is working correctly
                //ClientBase<TChannel>.factoryRefCache

                proxy = newProxy;

                newProxy.Open();

                if (string.Equals(lastSeenConnectionUri, dataCollectionServiceUri)) {
                    return true;
                }

                newProxy.ChannelContract.Ping();
                lastSeenConnectionUri = dataCollectionServiceUri;
                return true;
            } catch (Exception ex) {
                exception = ex;
                return false;
            }
        }
    }
}