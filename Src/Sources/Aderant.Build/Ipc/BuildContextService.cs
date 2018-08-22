using System;
using System.ServiceModel;

namespace Aderant.Build.Ipc {
    public class BuildContextService : IDisposable {
        ServiceHost host;
        private string id;

        public void Dispose() {
            StopListener();
            ((IDisposable)host)?.Dispose();
        }

        public void StartListener(string pipeId) {
            id = pipeId;
            host = new ServiceHost(typeof(ContextService));
            var namedPipeBinding = CreateBinding();

            host.AddServiceEndpoint(typeof(IContextService), namedPipeBinding, CreateAddress(pipeId));
            host.Open();

            Environment.SetEnvironmentVariable(WellKnownProperties.ContextFileName, pipeId, EnvironmentVariableTarget.Process);
        }

        private static string CreateAddress(string pipeId) {
            return $"net.pipe://localhost/_{pipeId}";
        }

        private static NetNamedPipeBinding CreateBinding() {
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

            using (var proxy = CreateProxy(id)) {
                proxy.Publish(context);
            }
        }

        internal static IContextServiceContract CreateProxy(string pipeId) {
            EndpointAddress endpointAddress = new EndpointAddress(CreateAddress(pipeId));
            var namedPipeBinding = CreateBinding();

            return new ContextServiceProxy(namedPipeBinding, endpointAddress);
        }
    }
}
