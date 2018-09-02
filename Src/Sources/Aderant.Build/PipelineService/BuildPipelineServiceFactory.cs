using System;
using System.ServiceModel;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    public class BuildPipelineServiceFactory : IDisposable {
        private static Lazy<IProxyAccessor> proxyForThisProcess = new Lazy<IProxyAccessor>(() => new ProxyAccessor(CreateAddress));

        ServiceHost host;
        private string id;

        internal static IProxyAccessor Instance {
            get {
                return proxyForThisProcess.Value;
            }
        }

        public void Dispose() {
            StopListener();
            ((IDisposable)host)?.Dispose();
        }

        public void StartListener(string pipeId) {
            id = pipeId;
            host = new ServiceHost(typeof(BuildPipelineServiceImpl));
            var namedPipeBinding = CreateBinding();

            var endpoint = host.AddServiceEndpoint(typeof(IBuildPipelineService), namedPipeBinding, CreateAddress(pipeId));
            endpoint.Behaviors.Add(new ProtoEndpointBehavior());

            host.Open();

            Environment.SetEnvironmentVariable(WellKnownProperties.ContextFileName, pipeId, EnvironmentVariableTarget.Process);
        }

        private static string CreateAddress(string pipeId) {
            return $"net.pipe://localhost/_{pipeId}";
        }

        internal static NetNamedPipeBinding CreateBinding() {
            NetNamedPipeBinding namedPipeBinding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
            namedPipeBinding.MaxReceivedMessageSize = Int32.MaxValue;
            return namedPipeBinding;
        }

        public void StopListener() {
            if (host != null && host.State == CommunicationState.Opened) {
                host.Close();
            }
        }

        public void Publish(BuildOperationContext context) {
            ErrorUtilities.IsNotNull(id, nameof(id));

            using (var proxy = Instance.GetProxy(id)) {
                proxy.Publish(context);
            }
        }
    }

    internal class ProxyAccessor : IProxyAccessor {

        private readonly Func<string, string> createAddress;

        public ProxyAccessor(Func<string, string> createAddress) {
            this.createAddress = createAddress;
        }

        public IBuildPipelineServiceContract GetProxy(string contextFileName) {
            if (string.IsNullOrEmpty(contextFileName)) {
                contextFileName = Environment.GetEnvironmentVariable(WellKnownProperties.ContextFileName);
            }

            ErrorUtilities.IsNotNull(contextFileName, nameof(contextFileName));

            return GetProxyInternal(contextFileName);
        }

        internal IBuildPipelineServiceContract GetProxyInternal(string pipeId) {
            EndpointAddress endpointAddress = new EndpointAddress(createAddress(pipeId));
            var namedPipeBinding = BuildPipelineServiceFactory.CreateBinding();

            return new BuildPipelineServiceProxy(namedPipeBinding, endpointAddress);
        }
    }

    internal interface IProxyAccessor {
        IBuildPipelineServiceContract GetProxy(string id);
    }
}
