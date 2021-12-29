using System.Net.Http;
using Microsoft.FSharp.Core;
using Paket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
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
            var member = typeof(NetUtils).Assembly.GetType("<StartupCode$Paket-Core>.$Paket.NetUtils").GetField("createHttpHandler@402", BindingFlags.Static | BindingFlags.NonPublic);
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
                    const string getSyntaxMethodName = "GetSyntax";
                    var getSyntaxMethod = typeof(UriParser).GetMethod(getSyntaxMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                    if (getSyntaxMethod == null) {
                        throw new MissingMethodException("UriParser", getSyntaxMethodName);
                    }

                    var uriParser = getSyntaxMethod.Invoke(null, new object[] { scheme });

                    const string setUpdatableFlagsMethodName = "SetUpdatableFlags";
                    var setUpdatableFlagsMethod = uriParser.GetType().GetMethod(setUpdatableFlagsMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (setUpdatableFlagsMethod == null) {
                        throw new MissingMethodException("UriParser", setUpdatableFlagsMethodName);
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
            if (string.Equals(uri.Host, "expertpackages-test.azurewebsites.net", StringComparison.OrdinalIgnoreCase)) {
                return new CertificateAuthenticationHandler(new PhysicalFileSystem());
            }

            try {
                if (string.Equals(uri.Host, new Uri(Constants.PackageServerUrlV3).Host, StringComparison.OrdinalIgnoreCase)) {
                    return new CertificateAuthenticationHandler(new PhysicalFileSystem());
                }
            } catch (System.UriFormatException) {
                // Perhaps a custom junk URL was used for testing, ignore it since the request will fail anyway
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

        private const string ThumbprintFile = "Certificate.txt";

        private static object fileLock = new object();
        private static X509Certificate2 clientCertificate;
        private static List<X509Certificate2> rejectedCertificates = new List<X509Certificate2>();

        private readonly IFileSystem2 fileSystem;

        public CertificateAuthenticationHandler(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpClientHandlerHelper.EnableAutoDecompression(this);
            HttpClientHandlerHelper.EnableTls(this);

            ClientCertificateOptions = ClientCertificateOption.Manual;

            if (request.Headers.CacheControl == null) {
                request.Headers.CacheControl = new CacheControlHeaderValue();
            }

            request.Headers.CacheControl.Public = true;

            // If this is the first request then close the connection so we can proffer up another certificate on 401
            if (clientCertificate == null) {
                request.Headers.ConnectionClose = true;
            } else {
                Debug.Assert(request.Headers.ConnectionClose.GetValueOrDefault() == false);

                request.Headers.ConnectionClose = false;
            }

            var certificates = GetClientCertificates().ToList();

            HttpResponseMessage response = null;
            foreach (var cert in certificates) {
                ClientCertificates.Clear();
                ClientCertificates.Add(cert);

                response = await base.SendAsync(request, cancellationToken);

                if (response.StatusCode != HttpStatusCode.Unauthorized) {
                    // /v3/index.json does not validate the certificate so ignore it
                    if (clientCertificate == null && !request.RequestUri.PathAndQuery.EndsWith("/v3/index.json", StringComparison.OrdinalIgnoreCase)) {
                        // Remember the valid certificate. First thread to get here wins.
                        Interlocked.CompareExchange(ref clientCertificate, cert, null);

                        // Poor separation of concerns here but performance is critical, if we can avoid a 401 on the first request we'll take that
                        // over a clean testable design.
                        SaveThumbprintFile(cert);
                    }

                    return response;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    lock (((ICollection)rejectedCertificates).SyncRoot) {
                        if (!rejectedCertificates.Contains(cert)) {
                            rejectedCertificates.Add(cert);
                        }
                    }
                }
            }

            return response;
        }

        private void SaveThumbprintFile(X509Certificate2 cert) {
            lock (fileLock) {
                fileSystem.WriteAllText(PathToThumbprint(), cert.Thumbprint);
            }
        }

        private static string PathToThumbprint() {
            return Path.Combine(WellKnownPaths.ProfileHome, ThumbprintFile);
        }

        private string ReadThumbprintFile() {
            lock (fileLock) {
                string pathToThumbprint = PathToThumbprint();
                if (fileSystem.FileExists(pathToThumbprint)) {
                    return fileSystem.ReadAllText(pathToThumbprint);
                }
            }

            return null;
        }

        private IEnumerable<X509Certificate2> GetClientCertificates() {
            var certificate = clientCertificate;

            if (certificate != null) {
                yield return certificate;
                yield break;
            }

            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser)) {
                store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

                var results = store.Certificates.Find(X509FindType.FindByApplicationPolicy, "1.3.6.1.5.5.7.3.2", true);

                var thumbprint = ReadThumbprintFile();

                List<X509Certificate2> matchingCertificates = new List<X509Certificate2>();

                foreach (var cert in results) {
                    if (cert.HasPrivateKey) {
                        var issuer = cert.GetNameInfo(X509NameType.SimpleName, true);

                        if (issuer != null && issuer.IndexOf("Aderant", StringComparison.OrdinalIgnoreCase) >= 0) {
                            if (string.Equals(cert.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)) {
                                matchingCertificates.Insert(0, cert);
                            } else {
                                matchingCertificates.Add(cert);
                            }
                        }
                    }
                }

                foreach (var cert in matchingCertificates) {
                    yield return cert;
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
                handler.SslProtocols |= SslProtocols.Tls12;
            }
        }
    }
}