using System;
using System.ServiceModel;

namespace Aderant.Build.PipelineService {
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
            var namedPipeBinding = BuildPipelineServiceHost.CreateBinding();

            return new BuildPipelineServiceProxy(namedPipeBinding, endpointAddress);
        }
    }
}
