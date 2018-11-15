using System;
using System.Management.Automation;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Resolvers;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "ExpertDependenciesForModule")]
    public class GetExpertDependenciesForModule : BuildCmdlet {
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();

        [Parameter(Mandatory = false, Position = 0)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string ModulesRootPath { get; set; }

        /// <summary>
        /// Gets or sets the directory to place the dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        [Parameter(Mandatory = false, Position = 2)]
        public string DependenciesDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 3)]
        public string DropPath { get; set; }

        [Parameter(Mandatory = true, Position = 4)]
        public string BuildScriptsDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 5)]
        public SwitchParameter Update { get; set; }

        [Parameter(Mandatory = false, Position = 6)]
        public SwitchParameter ShowOutdated { get; set; }

        [Parameter(Mandatory = false, Position = 7)]
        public SwitchParameter Force { get; set; }

        // This should be removed when we are fully migrated over. This exists for backwards compatibility with other versions of the build tools.
        [Parameter(Mandatory = false, Position = 8, DontShow = true)]
        [Obsolete]
        public SwitchParameter UseThirdPartyFromDrop { get; set; }

        [Parameter(Mandatory = false, Position = 9, HelpMessage = "Specifies the path the product manifest.")]
        public string ProductManifestPath { get; set; }

        [Parameter(Mandatory = false, Position = 10,  DontShow = true)]
        public string ManifestFile { get; set; }

        protected override void Process() {
            Logger.Info($"Module set to {ModuleName}");

            if (string.IsNullOrEmpty(ModuleName)) {
                ModuleName = ParameterHelper.GetCurrentModuleName(null, this.SessionState);
            }

            ExpertManifest productManifest = null;
            try {
                if (string.IsNullOrEmpty(ProductManifestPath)) {
                    ProductManifestPath = ParameterHelper.GetExpertManifestPath(ProductManifestPath, this.SessionState);
                }
                productManifest = ExpertManifest.Load(ProductManifestPath);
                productManifest.ModulesDirectory = ModulesRootPath;
            } catch (ArgumentException) {
                //git modules don't need ProductManifestPath
            }

            if (string.IsNullOrEmpty(DropPath)) {
                DropPath = ParameterHelper.GetDropPath(null, SessionState);
            }

            ResolverRequest request = new ResolverRequest(Logger, ModulesRootPath) {
                Force = Force,
                Update = Update
            };

            if (productManifest != null) {
                request.ModuleFactory = productManifest;
            }

            if (!string.IsNullOrEmpty(ModulesRootPath)) {
                request.AddModule(ModulesRootPath);
            }

            if (!string.IsNullOrEmpty(DependenciesDirectory)) {
                request.SetDependenciesDirectory(DependenciesDirectory);
            }

            ExpertModuleResolver moduleResolver;

            System.Diagnostics.Debugger.Launch();

            if (!string.IsNullOrWhiteSpace(ManifestFile)) {
                moduleResolver = new ExpertModuleResolver(new PhysicalFileSystem(ModulesRootPath, Logger), ManifestFile);
                request.RequiresThirdPartyReplication = true;
                request.Force = true;
            } else {
                moduleResolver = new ExpertModuleResolver(new PhysicalFileSystem(ModulesRootPath, Logger));
            }

            moduleResolver.AddDependencySource(DropPath, ExpertModuleResolver.DropLocation);

            Resolver resolver = new Resolver(Logger, moduleResolver, new NupkgResolver());
            resolver.ResolveDependencies(request, cancellationToken.Token);
        }

        protected override void StopProcessing() {
            base.StopProcessing();
            cancellationToken.Cancel();
        }
    }
}
