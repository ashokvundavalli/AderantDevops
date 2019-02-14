﻿using System;
using System.Globalization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {
    public class BuildPipelineServiceHost : IDisposable {

        private static string pipeId;
        private BuildPipelineServiceImpl dataService;

        ServiceHost host;

        public BuildPipelineServiceHost() {
            // Tear down any previous state (we never want to reconnect to the existing endpoint)
            pipeId = null;
        }

        public BuildOperationContext CurrentContext {
            get { return dataService.GetContext(); }
            set { dataService.Publish(value); }
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
                dataService = null;
                Environment.SetEnvironmentVariable(WellKnownProperties.ContextEndpoint, null, EnvironmentVariableTarget.Process);
            }
        }

        public void StartService(string pipeId) {
            if (host == null) {
                var address = CreateServerUri(pipeId, "0");

                dataService = new BuildPipelineServiceImpl();
                host = new ServiceHost(dataService, address);

                var namedPipeBinding = CreateNamedPipeBinding();

                var endpoint = host.AddServiceEndpoint(typeof(IBuildPipelineService), namedPipeBinding, address);
                endpoint.Behaviors.Add(new ProtoEndpointBehavior());

                host.Open();

                PipeId = pipeId;
                ServerUri = address;

                // Always set this as we might be invoked from an external application (like PowerShell) several times and so
                // need to update the value
                Environment.SetEnvironmentVariable(WellKnownProperties.ContextEndpoint, pipeId, EnvironmentVariableTarget.Process);
            }
        }

        public void StopListener() {
            if (host != null && host.State == CommunicationState.Opened) {
                host.Close();
            }
        }

        internal static Uri CreateServerUri(string namedPipeProcessToken, string namedPipeIdToken) {
            return CreateServerUri(namedPipeProcessToken, namedPipeIdToken, Environment.MachineName);
        }

        private static Uri CreateServerUri(string namedPipeProcessToken, string namedPipeIdToken, string machineName) {
            string endpoint = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}/{2}/{3}",
                Uri.UriSchemeNetPipe,
                machineName,
                namedPipeProcessToken,
                namedPipeIdToken);

            Uri uri;
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out uri) && uri != null) {
                return uri;
            }

            endpoint = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}/{2}/{3}",
                Uri.UriSchemeNetPipe,
                "localhost",
                namedPipeProcessToken,
                namedPipeIdToken);

            return new Uri(endpoint);
        }

        internal static Binding CreateNamedPipeBinding() {
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