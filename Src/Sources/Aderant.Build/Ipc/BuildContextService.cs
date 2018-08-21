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
            host = new ServiceHost(typeof(BuildCommunicator));
            var namedPipeBinding = CreateBinding();

            host.AddServiceEndpoint(typeof(IBuildCommunicator), namedPipeBinding, $"net.pipe://localhost/_{pipeId}");
            host.Open();
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

        internal static IBuildCommunicatorContract CreateProxy(string id) {
            EndpointAddress endpointAddress = new EndpointAddress($"net.pipe://localhost/_{id}");
            var namedPipeBinding = CreateBinding();

            return new BuildCommunicatorProxy(namedPipeBinding, endpointAddress);
        }
    }
}
