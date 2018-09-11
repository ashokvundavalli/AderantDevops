using System;
using System.ServiceModel;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    public class BuildPipelineServiceHost : IDisposable {
        private static string pipeId;

        ServiceHost host;

        public BuildOperationContext CurrentContext {
            get { return ((BuildPipelineServiceImpl)host.SingletonInstance).GetContext(); }
        }

        public static string PipeId {
            get { return pipeId ?? (pipeId = Environment.GetEnvironmentVariable(WellKnownProperties.ContextEndpoint)); }
            private set { pipeId = value; }
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
                var address = CreateAddress(pipeId);

                host = new ServiceHost(new BuildPipelineServiceImpl(), new Uri(address));
                var namedPipeBinding = CreateBinding();

                var endpoint = host.AddServiceEndpoint(typeof(IBuildPipelineServiceContract), namedPipeBinding, address);
                endpoint.Behaviors.Add(new ProtoEndpointBehavior());

                host.Open();

                PipeId = pipeId;
                Environment.SetEnvironmentVariable(WellKnownProperties.ContextEndpoint, pipeId, EnvironmentVariableTarget.Process);
            }
        }

        internal static string CreateAddress(string pipeId) {
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
            ErrorUtilities.IsNotNull(PipeId, nameof(PipeId));

            using (var proxy = BuildPipelineServiceProxy.Current) {
                proxy.Publish(context);
            }
        }
    }

}
