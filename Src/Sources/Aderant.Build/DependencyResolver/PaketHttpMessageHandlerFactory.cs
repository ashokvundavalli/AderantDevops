using System.Net.Http;
using Microsoft.FSharp.Core;
using Paket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Aderant.Build.DependencyResolver {
    internal class PaketHttpMessageHandlerFactory : FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler> {
        private readonly FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler> defaultHandler;

        private static bool isConfigured;

        public PaketHttpMessageHandlerFactory(FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler> defaultHandler) {
            this.defaultHandler = defaultHandler;
        }

        /// <summary>
        /// Works around the fact that Azure does not support the present of the Authorization header and SAS tokens at the same time.
        /// Paket gives us no customization points to remove the header so we are forced to inject ourselves into their pipeline via monkey patching
        /// </summary>
        public static void Configure() {
            if (isConfigured) {
                return;
            }
            // Monkey patch the built in HttpClient so we can control the headers. I need a shower.
            var member = typeof(NetUtils).Assembly.GetType("<StartupCode$Paket-Core>.$Paket.NetUtils").GetField("createHttpHandler@409", BindingFlags.Static | BindingFlags.NonPublic);
            var defaultHandler = (FSharpFunc<Tuple<string, FSharpOption<NetUtils.Auth>>, HttpMessageHandler>)member.GetValue(null);
            member.SetValue(null, new PaketHttpMessageHandlerFactory(defaultHandler));

            FixQuirks();

            isConfigured = true;
        }

        /// <summary>
        /// Implements DontUnescapePathDotsAndSlashes for PowerShell where we cannot change the app.config
        /// </summary>
        private static void FixQuirks() {
            foreach (string scheme in new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps }) {
                string url = scheme + "://foo/%2f/1";
                var uri = new Uri(url);

                if (!uri.PathAndQuery.Contains("%2f")) {
                    var getSyntaxMethod = typeof(UriParser).GetMethod("GetSyntax", BindingFlags.Static | BindingFlags.NonPublic);
                    if (getSyntaxMethod == null) {
                        throw new MissingMethodException("UriParser", "GetSyntax");
                    }

                    var uriParser = getSyntaxMethod.Invoke(null, new object[] { scheme });

                    var setUpdatableFlagsMethod = uriParser.GetType().GetMethod("SetUpdatableFlags", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (setUpdatableFlagsMethod == null) {
                        throw new MissingMethodException("UriParser", "SetUpdatableFlags");
                    }

                    setUpdatableFlagsMethod.Invoke(uriParser, new object[] { 0 });
                }
            }
        }

        public override HttpMessageHandler Invoke(Tuple<string, FSharpOption<NetUtils.Auth>> args) {
            if (args.Item1.IndexOf("blob.core.windows.net", StringComparison.OrdinalIgnoreCase) >= 0) {
                return new NoAuthorizationHeaderHandler();
            }

            if (args.Item1.IndexOf(".azureedge.net", StringComparison.OrdinalIgnoreCase) >= 0) {
                return new NoAuthorizationHeaderHandler();
            }

            var uri = new Uri(args.Item1);
            if (string.Equals(uri.Host, "expertpackages.azurewebsites.net", StringComparison.OrdinalIgnoreCase)) {

                return new CertificateAuthenticationHandler();
            }

#if DEBUG
            var httpMessageHandler = defaultHandler.Invoke(args);
            return new InspectionHandler(httpMessageHandler);
#else
            return defaultHandler.Invoke(args);
#endif

        }
    }

#if DEBUG
    internal class InspectionHandler : DelegatingHandler {
        public InspectionHandler(HttpMessageHandler httpMessageHandler) : base(httpMessageHandler) {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return base.SendAsync(request, cancellationToken);
        }
    }
#endif

    internal class CertificateAuthenticationHandler : HttpClientHandler {
        private static readonly Lazy<X509Certificate2[]> clientCertificates = new Lazy<X509Certificate2[]>(() => GetClientCertificates().ToArray());

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpClientHandlerHelper.EnableAutoDecompression(this);
            HttpClientHandlerHelper.EnableTls(this);

            ClientCertificateOptions = ClientCertificateOption.Manual;
            ClientCertificates.AddRange(clientCertificates.Value);

            return base.SendAsync(request, cancellationToken);
        }


        internal static IEnumerable<X509Certificate2> GetClientCertificates() {
            X509Certificate2Collection results;
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser)) {
                store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

                results = store.Certificates.Find(X509FindType.FindByApplicationPolicy, "1.3.6.1.5.5.7.3.2", true);
            }

            foreach (var cert in results) {
                if (cert.HasPrivateKey) {
                    var name = cert.GetNameInfo(X509NameType.SimpleName, true);
                    if (name != null && name.IndexOf("Aderant", StringComparison.OrdinalIgnoreCase) >= 0) {
                        yield return cert;
                    }
                }
            }
        }
    }

    internal class NoAuthorizationHeaderHandler : HttpClientHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpClientHandlerHelper.EnableAutoDecompression(this);
            HttpClientHandlerHelper.EnableTls(this);

            request.Headers.Authorization = null;

            // If we get x-ms-error-code: BlobNotFound we should probably do something smart on the next request
            return base.SendAsync(request, cancellationToken);
        }
    }

    internal class HttpClientHandlerHelper {
        public static void EnableAutoDecompression(HttpClientHandler handler) {
            handler.AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
        }

        public static void EnableTls(HttpClientHandler handler) {
            if (!handler.SslProtocols.HasFlag(SslProtocols.Tls12)) {
                handler.SslProtocols = handler.SslProtocols & SslProtocols.Tls12;
            }
        }
    }
}
