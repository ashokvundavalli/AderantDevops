using System;
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
        private VersionTracker versionTracker;

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
            versionTracker = new VersionTracker();

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

            using (var manager = new PackageManager(fs, logger)) {
                manager.Add(context, context.Modules.Select(DependencyRequirement.Create));
                manager.Restore();
            }

            var packages = fs.GetDirectories("packages").ToArray();

            // hack
            packages = packages.Where(p => p.IndexOf("Aderant.Build.Analyzer", StringComparison.OrdinalIgnoreCase) == -1).ToArray();

            var licenseText = CopyPackageContentToProductDirectory(context, fs, packages);

            fs.DeleteDirectory(fs.Root, true);

            return licenseText;
        }

        private IEnumerable<string> CopyPackageContentToProductDirectory(ProductAssemblyContext context, IFileSystem2 fs, string[] packages) {
            ConcurrentBag<string> licenseText = new ConcurrentBag<string>();

            string[] nupkgEntries = new[] { "lib", "content" };

            foreach (var packageDirectory in packages) {
                ExpertModule module = context.GetModuleByPackage(packageDirectory);
                if (module == null) {
                    //throw new InvalidOperationException(string.Format("Unable to resolve module for path: {0}. The module should be defined in the product manifest.", packageDirectory));
                }

                foreach (var packageDir in nupkgEntries) {
                    string nupkgDir = Path.Combine(packageDirectory, packageDir);

                    PhysicalFileSystem packageRelativeFs = new PhysicalFileSystem(fs.GetFullPath(nupkgDir));

                    if (fs.DirectoryExists(nupkgDir)) {
                        var packageName = Path.GetDirectoryName(packageDirectory);

                        if (module != null) {
                            if (context.IsRootItem(module)) {
                                RootItemHandler processor = new RootItemHandler(packageRelativeFs) {
                                    Module = module,
                                };

                                processor.MoveContent(context, fs.GetFullPath(nupkgDir));

                                versionTracker.FileSystem = fs;
                                versionTracker.RecordVersion(module, fs.GetFullPath(packageDirectory));
                                continue;
                            }
                        }

                        ReadLicenseText(fs, nupkgDir, packageName, licenseText);

                        string relativeDirectory;
                        if (module != null) {
                            relativeDirectory = context.ResolvePackageRelativeDirectory(module);
                        } else {
                            relativeDirectory = context.ProductDirectory;
                        }
                        
                        logger.Info("Copying {0} ==> {1}", nupkgDir, relativeDirectory);
                        packageRelativeFs.MoveDirectory(fs.GetFullPath(nupkgDir), relativeDirectory);
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
}