using System;
using System.ServiceModel;

namespace Aderant.Build.PipelineService {
    internal class ProxyAccessor : IProxyAccessor {

        private readonly Func<string, string> createAddress;

        public ProxyAccessor(Func<string, string> createAddress) {
            this.createAddress = createAddress;
        }

        public IBuildPipelineServiceContract GetProxy(string contextEndpoint) {
            if (string.IsNullOrEmpty(contextEndpoint)) {
                contextEndpoint = Environment.GetEnvironmentVariable(WellKnownProperties.ContextEndpoint);
            }

            ErrorUtilities.IsNotNull(contextEndpoint, nameof(contextEndpoint));

            return GetProxyInternal(contextEndpoint);
        }

        internal IBuildPipelineServiceContract GetProxyInternal(string pipeId) {
            EndpointAddress endpointAddress = new EndpointAddress(createAddress(pipeId));
            var namedPipeBinding = BuildPipelineServiceHost.CreateBinding();

            return new BuildPipelineServiceProxy(namedPipeBinding, endpointAddress);
        }
    }
}
