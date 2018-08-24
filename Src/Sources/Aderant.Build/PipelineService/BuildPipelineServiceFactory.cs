using System;
using System.ServiceModel;

namespace Aderant.Build.PipelineService {
    public class BuildPipelineServiceFactory : IDisposable {
        ServiceHost host;
        private string id;

        public void Dispose() {
            StopListener();
            ((IDisposable)host)?.Dispose();
        }

        public void StartListener(string pipeId) {
            id = pipeId;
            host = new ServiceHost(typeof(BuildPipelineServiceImpl));
            var namedPipeBinding = CreateBinding();

            var endpoint = host.AddServiceEndpoint(typeof(IBuildPipelineService), namedPipeBinding, CreateAddress(pipeId));
            endpoint.Behaviors.Add(new ProtoBuf.ServiceModel.ProtoEndpointBehavior());

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
            System.Diagnostics.Debugger.Launch();
            ErrorUtilities.IsNotNull(id, nameof(id));

            using (var proxy = CreateProxy(id)) {
                proxy.Publish(context);
            }
        }

        internal static IBuildPipelineServiceContract CreateProxy(string pipeId) {
            EndpointAddress endpointAddress = new EndpointAddress(CreateAddress(pipeId));
            var namedPipeBinding = CreateBinding();
            

            return new BuildPipelineServiceProxy(namedPipeBinding, endpointAddress);
        }
    }
}
