using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver.Resolvers;
using Aderant.Build.Logging;

namespace Aderant.Build.DependencyResolver {
    internal class ResolverWorkflow {
        private XDocument configurationXml;
        private HashSet<string> enabledResolvers;

        private ResolverRequest request;

        public ResolverWorkflow(ILogger logger) {
            Logger = logger;
        }

        public ILogger Logger { get; }
        public bool Force { get; set; }
        public bool Update { get; set; }
        public string DropPath { get; set; }

        public ResolverRequest Request {
            get {
                if (request == null) {
                    this.request = new ResolverRequest(Logger) {
                        Force = Force,
                        Update = Update
                    };
                }

                return request;
            }
        }

        public string ModulesRootPath { get; set; }

        public string ManifestFile { get; set; }

        public string DependenciesDirectory { get; set; }

        /// <summary>
        /// Settings document that controls resolver options.
        /// </summary>
        public XDocument ConfigurationXml {
            get { return configurationXml; }
            set {
                configurationXml = value;
                InitializeResolvers();
            }
        }

        public void Run(CancellationToken cancellationToken, bool enableVerboseLogging) {
            if (!string.IsNullOrWhiteSpace(DependenciesDirectory)) {
                Request.SetDependenciesDirectory(DependenciesDirectory);
            }

            List<IDependencyResolver> resolvers = new List<IDependencyResolver>();
            if (IncludeResolver(nameof(ExpertModuleResolver))) {

                ExpertModuleResolver moduleResolver;
                if (!string.IsNullOrWhiteSpace(ManifestFile)) {
                    moduleResolver = new ExpertModuleResolver(new PhysicalFileSystem(ModulesRootPath, Logger), ManifestFile);
                    Request.RequiresThirdPartyReplication = true;
                    Request.Force = true;
                } else {
                    moduleResolver = new ExpertModuleResolver(new PhysicalFileSystem(ModulesRootPath, Logger));
                }

                moduleResolver.AddDependencySource(DropPath, ExpertModuleResolver.DropLocation);
                resolvers.Add(moduleResolver);

            }

            if (IncludeResolver(nameof(NupkgResolver))) {
                resolvers.Add(new NupkgResolver());
            }

            Resolver resolver = new Resolver(Logger, resolvers.ToArray());
            resolver.ResolveDependencies(Request, cancellationToken, enableVerboseLogging);
        }

        private bool IncludeResolver(string name) {
            if (enabledResolvers != null && enabledResolvers.Count == 0) {
                return true;
            }

            if (enabledResolvers == null) {
                InitializeResolvers();
            }

            var includeResolver = enabledResolvers != null && enabledResolvers.Contains(name);
            if (!includeResolver) {
                Logger.Info($"Resolver '{name}' is not enabled.");
            }

            return includeResolver;
        }

        private void InitializeResolvers() {
            XDocument xml = ConfigurationXml;
            if (xml != null) {
                XElement element;
                if (xml.Root.Name.LocalName.Equals("DependencyResolvers")) {
                    element = xml.Root;
                } else {
                    element = xml.Root.Element("DependencyResolvers");
                }

                enabledResolvers = ParseProperties(element);
                return;
            }

            enabledResolvers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> ParseProperties(XElement items) {
            var resolvers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in items.Descendants()) {
                if (element.Name.LocalName == "NupkgResolver") {
                    resolvers.Add(element.Name.LocalName);

                    XElement validationElement = element.Descendants("ValidatePackageConstraints").FirstOrDefault();
                    bool validatePackageConstraints;
                    if (validationElement != null && bool.TryParse(validationElement.Value, out validatePackageConstraints)) {
                        Request.ValidatePackageConstraints = validatePackageConstraints;
                    }

                    if (element.Name.LocalName == "ExpertModuleResolver") {
                        resolvers.Add(element.Name.LocalName);
                    }
                }
            }

            return resolvers;
        }
    }
}
