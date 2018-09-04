using System;
using System.ServiceModel;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    public class BuildPipelineServiceHost : IDisposable {
        private static Lazy<IProxyAccessor> proxyForThisProcess = new Lazy<IProxyAccessor>(() => new ProxyAccessor(CreateAddress));

        ServiceHost host;
        private string id;

        internal static IProxyAccessor Instance {
            get { return proxyForThisProcess.Value; }
        }

        internal BuildOperationContext CurrentContext {
            get { return ((BuildPipelineServiceImpl)host.SingletonInstance).GetContext(); }
        }

        public void Dispose() {
            StopListener();

            try {
                ((IDisposable)host)?.Dispose();
                host = null;
            } catch {

            }
        }

        public void StartListener(string pipeId) {
            if (host == null) {
                id = pipeId;

                var address = CreateAddress(pipeId);

                host = new ServiceHost(new BuildPipelineServiceImpl(), new Uri(address));
                var namedPipeBinding = CreateBinding();

                var endpoint = host.AddServiceEndpoint(typeof(IBuildPipelineService), namedPipeBinding, address);
                endpoint.Behaviors.Add(new ProtoEndpointBehavior());

                host.Open();

                Environment.SetEnvironmentVariable(WellKnownProperties.ContextEndpoint, pipeId, EnvironmentVariableTarget.Process);
            }
        }

        private static string CreateAddress(string pipeId) {
            return $"net.pipe://localhost/_{pipeId}";
        }

        internal static NetNamedPipeBinding CreateBinding() {
            NetNamedPipeBinding namedPipeBinding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
            namedPipeBinding.MaxReceivedMessageSize = Int32.MaxValue;
            //namedPipeBinding.TransferMode = TransferMode.Streamed;
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

}
