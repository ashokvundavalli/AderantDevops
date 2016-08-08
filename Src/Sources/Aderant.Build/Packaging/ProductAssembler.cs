using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {
    internal class ProductAssembler {
        private readonly ILogger logger;
        private ExpertManifest manifest;

        public ProductAssembler(string productManifestPath, ILogger logger) {
            this.logger = logger;
            this.manifest = ExpertManifest.Load(productManifestPath);
        }

        public void AssembleProduct(IEnumerable<string> modules, IEnumerable<string> buildOutputs, string productDirectory) {
            IEnumerable<ExpertModule> resolvedModules = modules.Select(m => manifest.GetModule(m));

            AssembleProduct(new ProductAssemblyContext {
                Modules = resolvedModules,
                BuildOutputs = buildOutputs,
                ProductDirectory = productDirectory
            }).Wait();
        }

        private async Task AssembleProduct(ProductAssemblyContext context) {
            await RetrievePackages(context);

            RetrieveBuildOutputs(context);
        }

        private void RetrieveBuildOutputs(ProductAssemblyContext context) {
            var fs = new PhysicalFileSystem(context.ProductDirectory);

            Parallel.ForEach(context.BuildOutputs, folder => {
                logger.Info("Copying {0} ==> {1}", folder, context.ProductDirectory);

                fs.CopyDirectory(folder, context.ProductDirectory);
            });
        }

        private async Task RetrievePackages(ProductAssemblyContext context) {
            var fs = new PhysicalFileSystem(Path.Combine(context.ProductDirectory, "package.temp"));
            var manager = new PackageManager(fs, logger);

            manager.Add(context, context.Modules);
            await manager.Restore();

            var packages = fs.GetDirectories("packages").ToArray();

            CopyPackageContentToProductDirectory(context, fs, packages);

            fs.DeleteDirectory(fs.Root, true);
        }

        private void CopyPackageContentToProductDirectory(ProductAssemblyContext context, PhysicalFileSystem fs, string[] packages) {
            Parallel.ForEach(packages, packageDirectory => {
                string lib = Path.Combine(packageDirectory, "lib");
                if (fs.DirectoryExists(lib)) {
                    logger.Info("Copying {0} ==> {1}", lib, context.ProductDirectory);

                    fs.CopyDirectory(lib, context.ProductDirectory);
                }
            });
        }
    }

    internal class ProductAssemblyContext : IPackageContext {
        public IEnumerable<ExpertModule> Modules { get; set; }
        public string ProductDirectory { get; set; }

        public bool IncludeDevelopmentDependencies {
            get { return false; }
        }

        public bool AllowExternalPackages {
            get { return false; } 
        }

        public IEnumerable<string> BuildOutputs { get; set; }
    }
}