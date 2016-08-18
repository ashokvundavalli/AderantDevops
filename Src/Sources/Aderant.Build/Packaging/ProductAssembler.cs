using System;
using System.Collections.Concurrent;
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

        public IProductAssemblyResult AssembleProduct(IEnumerable<string> modules, IEnumerable<string> buildOutputs, string productDirectory) {
            IEnumerable<ExpertModule> resolvedModules = modules.Select(m => manifest.GetModule(m));

            var operation = AssembleProduct(new ProductAssemblyContext {
                Modules = resolvedModules,
                BuildOutputs = buildOutputs,
                ProductDirectory = productDirectory
            });

            operation.Wait();

            return operation.Result;
        }

        private async Task<IProductAssemblyResult> AssembleProduct(ProductAssemblyContext context) {
            RetrieveBuildOutputs(context);

            IEnumerable<string> licenseText = await RetrievePackages(context);

            return new ProductAssemblyResult {
                ThirdPartyLicenses = licenseText
            };
        }

        private void RetrieveBuildOutputs(ProductAssemblyContext context) {
            var fs = new PhysicalFileSystem(context.ProductDirectory);

            Parallel.ForEach(context.BuildOutputs, folder => {
                logger.Info("Copying {0} ==> {1}", folder, context.ProductDirectory);

                fs.CopyDirectory(folder, context.ProductDirectory);
            });
        }

        private async Task<IEnumerable<string>> RetrievePackages(ProductAssemblyContext context) {
            var fs = new PhysicalFileSystem(Path.Combine(context.ProductDirectory, "package." + Path.GetRandomFileName()));
            var manager = new PackageManager(fs, logger);

            manager.Add(context, context.Modules);
            await manager.Restore();

            var packages = fs.GetDirectories("packages").ToArray();

            var licenseText = CopyPackageContentToProductDirectory(context, fs, packages);

            fs.DeleteDirectory(fs.Root, true);

            return licenseText;
        }

        private IEnumerable<string> CopyPackageContentToProductDirectory(ProductAssemblyContext context, PhysicalFileSystem fs, string[] packages) {
            ConcurrentBag<string> licenseText = new ConcurrentBag<string>();

            Parallel.ForEach(packages, packageDirectory => {
                string lib = Path.Combine(packageDirectory, "lib");
                if (fs.DirectoryExists(lib)) {

                    var packageName = Path.GetDirectoryName(packageDirectory);

                    ReadLicenseText(fs, lib, packageName, licenseText);

                    logger.Info("Copying {0} ==> {1}", lib, context.ProductDirectory);

                    fs.CopyDirectory(lib, context.ProductDirectory);
                }
            });

            return licenseText;
        }

        private static void ReadLicenseText(PhysicalFileSystem fs, string lib, string packageName, ConcurrentBag<string> licenseText) {
            IEnumerable<string> licenses = fs.GetFiles(lib, "*license*txt", true);
            foreach (var licenseFile in licenses) {
                using (Stream stream = fs.OpenFile(licenseFile)) {
                    using (var reader = new StreamReader(stream)) {

                        licenseText.Add(new string('=', 80));
                        licenseText.Add(packageName);
                        licenseText.Add(new string('=', 80));
                        licenseText.Add(reader.ReadToEnd());
                    }
                }
            }
        }
    }

    public interface IProductAssemblyResult {
        IEnumerable<string> ThirdPartyLicenses { get; }
    }

    public sealed class ProductAssemblyResult : IProductAssemblyResult {
        public IEnumerable<string> ThirdPartyLicenses { get; internal set; }
    }

    public sealed class ProductAssemblyContext : IPackageContext {
        public IEnumerable<ExpertModule> Modules { get; internal set; }
        public string ProductDirectory { get; internal set; }

        public bool IncludeDevelopmentDependencies {
            get { return false; }
        }

        public bool AllowExternalPackages {
            get { return false; }
        }

        public IEnumerable<string> BuildOutputs { get; internal set; }
    }
}