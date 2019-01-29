using System.Diagnostics;
using System.IO;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Resolvers;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class GetDependencies : Task, ICancelableTask {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public string ModulesRootPath { get; set; }

        public string DropPath { get; set; }

        public string ProductManifest { get; set; }

        /// <summary>
        /// Gets or sets the name of the module consuming the dependencies.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName { get; set; }

        /// <summary>
        /// Gets or sets the directory to place the dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        public string DependenciesDirectory { get; set; }

        /// <summary>
        /// Gets or sets the modules in the build set.
        /// </summary>
        /// <value>
        /// The modules in build.
        /// </value>
        public ITaskItem[] ModulesInBuild { get; set; }

        /// <summary>
        /// Gets or sets the type of the build. For example Build All, Continuous Integration or other.
        /// </summary>
        /// <value>The type of the build.</value>
        public string BuildType { get; set; }

        public override bool Execute() {
            ModulesRootPath = Path.GetFullPath(ModulesRootPath);

            var logger = new BuildTaskLogger(this);

            ResolverRequest request = new ResolverRequest(logger);

            if (ProductManifest != null) {
                ProductManifest = Path.GetFullPath(ProductManifest);
                ExpertManifest productManifest = ExpertManifest.Load(ProductManifest);
                productManifest.ModulesDirectory = ModulesRootPath;
                request.ModuleFactory = productManifest;
            }

            LogParameters();

            request.SetDependenciesDirectory(DependenciesDirectory);
            request.DirectoryContext = BuildType;

            if (!string.IsNullOrEmpty(ModuleName)) {
                request.AddModule(ModuleName);
            }

            if (ModulesInBuild != null) {
                foreach (ITaskItem module in ModulesInBuild) {
                    request.AddModule(module.ItemSpec);
                }
            }

            ExpertModuleResolver moduleResolver = new ExpertModuleResolver(new PhysicalFileSystem(ModulesRootPath));
            moduleResolver.AddDependencySource(DropPath, ExpertModuleResolver.DropLocation);

            Resolver resolver = new Resolver(logger, moduleResolver, new NupkgResolver());
            resolver.ResolveDependencies(request, cancellationTokenSource.Token);

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Attempts to cancel this instance.
        /// </summary>
        public void Cancel() {
            if (cancellationTokenSource != null) {
                // Signal the cancellation token that we want to abort the async task
                cancellationTokenSource.Cancel();
            }
        }

        private void LogParameters() {
            Log.LogMessage(MessageImportance.Normal, "ModulesRootPath: " + ModulesRootPath, null);
            Log.LogMessage(MessageImportance.Normal, "DropPath: " + DropPath, null);
            Log.LogMessage(MessageImportance.Normal, "ProductManifest: " + ProductManifest, null);
        }
    }
}
