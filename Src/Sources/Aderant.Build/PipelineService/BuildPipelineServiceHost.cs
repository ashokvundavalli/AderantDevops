using System;
using System.Globalization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    public class BuildPipelineServiceHost : IDisposable {

        private static string pipeId;

        ServiceHost host;

        public BuildPipelineServiceHost() {
            // Tear down any previous state (we never want to reconnect to the existing endpoint)
            pipeId = null;
        }

        public BuildOperationContext CurrentContext {
            get { return ((BuildPipelineServiceImpl)host.SingletonInstance).GetContext(); }
        }

        public static string PipeId {
            get { return pipeId ?? (pipeId = Environment.GetEnvironmentVariable(WellKnownProperties.ContextEndpoint)); }
            private set { pipeId = value; }
        }

        public Uri ServerUri { get; set; }

        public void Dispose() {
            StopListener();

            try {
                ((IDisposable)host)?.Dispose();
            } catch {
                // ignored
            } finally {
                host = null;
            }
        }

        public void StartListener(string pipeId) {

            if (host == null) {
                var address = CreateServerUri(pipeId, "0");

                host = new ServiceHost(new BuildPipelineServiceImpl(), address);
                var namedPipeBinding = CreateServerBinding();

                var endpoint = host.AddServiceEndpoint(typeof(IBuildPipelineService), namedPipeBinding, address);
                endpoint.Behaviors.Add(new ProtoEndpointBehavior());

                host.Open();

                PipeId = pipeId;
                ServerUri = address;
                Environment.SetEnvironmentVariable(WellKnownProperties.ContextEndpoint, pipeId, EnvironmentVariableTarget.Process);
            }
        }

        public void StopListener() {
            if (host != null && host.State == CommunicationState.Opened) {
                host.Close();
            }
        }

        public void Publish(BuildOperationContext context) {
            ErrorUtilities.IsNotNull(PipeId, nameof(PipeId));

            using (var proxy = BuildPipelineServiceClient.Current) {
                proxy.Publish(context);
            }
        }

        internal static Uri CreateServerUri(string namedPipeProcessToken, string namedPipeIdToken) {
            return CreateServerUri(namedPipeProcessToken, namedPipeIdToken, Environment.MachineName);
        }

        internal static Uri CreateServerUri(string namedPipeProcessToken, string namedPipeIdToken, string machineName) {
            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}/{2}/{3}",
                Uri.UriSchemeNetPipe,
                machineName,
                namedPipeProcessToken,
                namedPipeIdToken);
            Uri uri;

            if (Uri.TryCreate(text, UriKind.Absolute, out uri) && uri != null) {
                return uri;
            }

            text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}/{2}/{3}",
                Uri.UriSchemeNetPipe,
                "localhost",
                namedPipeProcessToken,
                namedPipeIdToken);

            return new Uri(text);
        }

        internal static Binding CreateServerBinding() {
            return new NetNamedPipeBinding {
                MaxReceivedMessageSize = Int32.MaxValue,
                MaxBufferSize = Int32.MaxValue,
                ReaderQuotas = {
                    MaxArrayLength = Int32.MaxValue,
                    MaxStringContentLength = Int32.MaxValue,
                    MaxDepth = Int32.MaxValue
                },
                ReceiveTimeout = TimeSpan.MaxValue,
                SendTimeout = TimeSpan.FromMinutes(1)
            };
        }
    }
}
