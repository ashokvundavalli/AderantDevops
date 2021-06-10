using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;

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

        private readonly object proxyLock = new object();

        internal BuildPipelineServiceClient(string dataCollectionServiceUri) {
            this.dataCollectionServiceUri = dataCollectionServiceUri;
        }

        /// <summary>
        /// The wrapped data service proxy
        /// </summary>
        internal BuildPipelineServiceProxy InnerProxy { get; set; }

        internal IBuildPipelineService Contract {
            get { return InnerProxy.ChannelContract; }
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            InvokeServiceAction((service, a) => service.AssociateArtifacts(a), artifacts);
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return InvokeServiceAction((service, _) => service.GetAssociatedArtifacts(), (object)null);
        }

        public void Publish(BuildOperationContext context) {
            InvokeServiceAction((service, a) => service.Publish(a), context);
        }

        public Task PublishAsync(BuildOperationContext context) {
            return InvokeServiceAction((service, a) => service.PublishAsync(a), context);
        }

        public BuildOperationContext GetContext() {
            return InvokeServiceAction((service, a) => service.GetContext(), (object)null);
        }

        public BuildOperationContext GetContext(QueryOptions options) {
            return InvokeServiceAction((service, a) => service.GetContext(a), options);
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            InvokeServiceAction((service, a) => service.RecordProjectOutputs(a), snapshot);
        }

        public void RecordImpactedProjects(IEnumerable<string> impactedProjects) {
            InvokeServiceAction((service, a) => service.RecordImpactedProjects(a), impactedProjects);
        }

        public void RecordRelatedFiles(Dictionary<string, List<string>> relatedFiles) {
            InvokeServiceAction((service, a) => service.RecordRelatedFiles(a), relatedFiles);
        }

        public Task RecordRelatedFilesAsync(Dictionary<string, List<string>> relatedFiles) {
            return InvokeServiceAction((service, a) => service.RecordRelatedFilesAsync(a), relatedFiles);
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return InvokeServiceAction((service, a) => service.GetProjectOutputs(a), container);
        }

        public IEnumerable<string> GetImpactedProjects() {
            return InvokeServiceAction((service, a) => service.GetImpactedProjects(), (object)null);
        }

        public Dictionary<string, List<string>> GetRelatedFiles() {
            return InvokeServiceAction((service, a) => service.GetRelatedFiles(), (object)null);
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectSnapshots() {
            return InvokeServiceAction((service, a) => service.GetProjectSnapshots(), (object)null);
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            InvokeServiceAction((service, a) => service.RecordArtifacts(a.Item1, a.Item2), ValueTuple.Create(container, manifests));
        }

        public void PutVariable(string scope, string variableName, string value) {
            InvokeServiceAction((service, a) => service.PutVariable(a.Item1, a.Item2, a.Item3), ValueTuple.Create(scope, variableName, value));
        }

        public string GetVariable(string scope, string variableName) {
            return InvokeServiceAction((service, a) => service.GetVariable(a.Item1, a.Item2), ValueTuple.Create(scope, variableName));
        }

        public void TrackProject(OnDiskProjectInfo onDiskProject) {
            InvokeServiceAction((service, a) => service.TrackProject(a), onDiskProject);
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects() {
            return InvokeServiceAction((service, a) => service.GetTrackedProjects(), (object)null);
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids) {
            return InvokeServiceAction((service, a) => service.GetTrackedProjects(a), ids);
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            return InvokeServiceAction((service, a) => service.GetArtifactsForContainer(a), container);
        }

        public object[] Ping() {
            return InvokeServiceAction((service, a) => service.Ping(), (object)null);
        }

        public void SetStatus(string status, string reason) {
            InvokeServiceAction((service, a) => service.SetStatus(a.Item1, a.Item2), ValueTuple.Create(status, reason));
        }

        /// <summary>
        /// Notifies listeners of build progress.
        /// </summary>
        public void SetProgress(string currentOperation, string activity, string statusDescription) {
            InvokeServiceAction((service, a) => service.SetProgress(a.Item1, a.Item2, a.Item3), ValueTuple.Create(currentOperation, activity, statusDescription));
        }

        public BuildStateFile GetStateFile(string container) {
            return InvokeServiceAction((service, a) => service.GetStateFile(a), container);
        }

        public void TrackInputFileDependencies(string solutionRoot, IReadOnlyCollection<TrackedInputFile> fileDependencies) {
            InvokeServiceAction((service, a) => service.TrackInputFileDependencies(a.Item1, a.Item2), ValueTuple.Create(solutionRoot, fileDependencies));
        }

        public IReadOnlyCollection<TrackedInputFile> ClaimTrackedInputFiles(string tag) {
            return InvokeServiceAction((service, a) => service.ClaimTrackedInputFiles(a), tag);
        }

        public void AddBuildDirectoryContributor(BuildDirectoryContribution buildDirectoryContribution) {
            InvokeServiceAction((service, a) => service.AddBuildDirectoryContributor(a), buildDirectoryContribution);
        }

        public IReadOnlyCollection<BuildDirectoryContribution> GetContributors() {
            return InvokeServiceAction((service, a) => service.GetContributors(), (object)null);
        }

        public void Dispose() {
            var proxy = InnerProxy;

            if (proxy != null) {
                lock (proxyLock) {
                    try {
                        IDisposable disposable = proxy;
                        disposable.Dispose();
                        InnerProxy = null;
                    } catch {
                    }
                }
            }
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
            var proxy = GetProxy(BuildPipelineServiceHost.PipeId);
            return proxy;
        }

        /// <summary>
        /// Convenience factory method for creating a client from a partial named pipe address
        /// </summary>
        /// <param name="pipeId"></param>
        private static IBuildPipelineService CreateFromPipeId(string pipeId) {
            var client = new BuildPipelineServiceClient(BuildPipelineServiceHost.CreateServerUri(pipeId, "0").AbsoluteUri);
            return client;
        }

        private TOutput InvokeServiceAction<TInput, TOutput>(Func<IBuildPipelineService, TInput, TOutput> action, TInput input) {
            EnsureInitialized();

            BuildPipelineServiceProxy proxy = InnerProxy;

            if (proxy != null && proxy.State == CommunicationState.Opened) {
                try {
                    return action(proxy.ChannelContract, input);
                } catch (FaultException ex) {
                    throw ExceptionConverter.ConvertException(ex);
                }
            }

            throw new CommunicationException("Proxy is communication state is invalid");
        }

        private void InvokeServiceAction<T>(Action<IBuildPipelineService, T> action, T data) {
            EnsureInitialized();

            BuildPipelineServiceProxy proxy = InnerProxy;
            if (proxy != null && proxy.State == CommunicationState.Opened) {
                try {
                    action(proxy.ChannelContract, data);
                    return;
                } catch (FaultException ex) {
                    throw ExceptionConverter.ConvertException(ex);
                }
            }

            throw new CommunicationException("Proxy is communication state is invalid");
        }

        private void EnsureInitialized() {
            object syncInitialization = proxyLock;

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
                throw new BuildPlatformException(string.Format("Could not connect to data service within the available time {0}. Reason:{1}", timeout, ex.Message), ex);
            }

            InnerProxy = proxy;
        }

        private bool TestConnection(Binding binding, EndpointAddress address, out BuildPipelineServiceProxy proxy, out Exception exception) {
            proxy = null;
            exception = null;

            try {
                if (InnerProxy != null) {
                    proxy = InnerProxy;
                } else {
                    var newProxy = new BuildPipelineServiceProxy(binding, address);
                    proxy = newProxy;
                }
                // Ref cache should only have 1 member in it if WCF caching is working correctly
                //ClientBase<IBuildPipelineService>.factoryRefCache

                if (proxy.State != CommunicationState.Opened) {
                    proxy.Open();
                }

                if (string.Equals(lastSeenConnectionUri, dataCollectionServiceUri)) {
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