using System.Net.Http;
using Microsoft.FSharp.Core;
using Paket;
using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Aderant.Build.DependencyResolver {
    internal class PaketHttpMessageHandlerFactory : FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler> {
        private readonly FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler> defaultHandler;
        private static bool IsConfigured;

        public PaketHttpMessageHandlerFactory(FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler> defaultHandler) {
            this.defaultHandler = defaultHandler;
        }

        /// <summary>
        /// Works around the fact that Azure does not support the present of the Authorization header and SAS tokens at the same time.
        /// Paket gives us no customization points to remove the header so we are forced to inject ourselves into their pipeline via monkey patching
        /// </summary>
        public static void Configure() {
            if (IsConfigured) {
                return;
            }
            // Monkey patch the built in HttpClient so we can control the headers. I need a shower.
            var member = typeof(NetUtils).Assembly.GetType("<StartupCode$Paket-Core>.$Paket.NetUtils").GetField("createHttpHandler@409", BindingFlags.Static | BindingFlags.NonPublic);
            var defaultHandler = (FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler>)member.GetValue(null);
            member.SetValue(null, new PaketHttpMessageHandlerFactory(defaultHandler));

            IsConfigured = true;
        }

        public override HttpMessageHandler Invoke(Tuple<string, FSharpOption<NetUtils.Auth>> args) {
            if (args.Item1.IndexOf("blob.core.windows.net", StringComparison.OrdinalIgnoreCase) >= 0) {
                return new NoAuthorizationHeaderHandler();
            }

            return defaultHandler.Invoke(args);
        }
    }

    internal class NoAuthorizationHeaderHandler : HttpClientHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);

            request.Headers.Authorization = null;

            // If we get x-ms-error-code: BlobNotFound we should probably do something smart on the next request
            return base.SendAsync(request, cancellationToken);
        }

        public static HttpMessageHandler Default { get; } = new NoAuthorizationHeaderHandler();
    }
}