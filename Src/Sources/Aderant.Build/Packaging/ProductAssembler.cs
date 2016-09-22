using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            return operation;
        }

        private IProductAssemblyResult AssembleProduct(ProductAssemblyContext context) {
            RetrieveBuildOutputs(context);

            IEnumerable<string> licenseText = RetrievePackages(context);

            return new ProductAssemblyResult {
                ThirdPartyLicenses = licenseText
            };
        }

        private void RetrieveBuildOutputs(ProductAssemblyContext context) {
            var fs = new PhysicalFileSystem(context.ProductDirectory);

            foreach (var folder in context.BuildOutputs) {
                logger.Info("Copying {0} ==> {1}", folder, context.ProductDirectory);

                fs.CopyDirectory(folder, context.ProductDirectory);
            }
        }

        private IEnumerable<string> RetrievePackages(ProductAssemblyContext context) {
            var fs = new RetryingPhysicalFileSystem(Path.Combine(context.ProductDirectory, "package." + Path.GetRandomFileName()));
            var manager = new PackageManager(fs, logger);

            manager.Add(context, context.Modules);
            manager.Restore();

            var packages = fs.GetDirectories("packages").ToArray();

            var licenseText = CopyPackageContentToProductDirectory(context, fs, packages);

            fs.DeleteDirectory(fs.Root, true);

            return licenseText;
        }

        private IEnumerable<string> CopyPackageContentToProductDirectory(ProductAssemblyContext context, IFileSystem2 fs, string[] packages) {
            ConcurrentBag<string> licenseText = new ConcurrentBag<string>();

            string[] nupkgEntries = new[] { "lib", "content" };

            foreach (var packageDirectory in packages) {
                foreach (var packageDir in nupkgEntries) {
                    string nupkgDir = Path.Combine(packageDirectory, packageDir);

                    if (fs.DirectoryExists(nupkgDir)) {
                        var packageName = Path.GetDirectoryName(packageDirectory);

                        ReadLicenseText(fs, nupkgDir, packageName, licenseText);

                        logger.Info("Copying {0} ==> {1}", nupkgDir, context.ProductDirectory);

                        fs.CopyDirectory(nupkgDir, context.ProductDirectory);
                    }
                }
            }

            return licenseText;
        }

        private static void ReadLicenseText(IFileSystem2 fs, string lib, string packageName, ConcurrentBag<string> licenseText) {
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