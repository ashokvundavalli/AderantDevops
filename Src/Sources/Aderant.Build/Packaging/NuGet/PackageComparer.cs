using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Tasks;

namespace Aderant.Build.Packaging.NuGet {
    internal class PackageComparer {
        private readonly IFileSystem2 fileSystem;
        private readonly Logging.ILogger logger;

        //   private bool packageExists;

        public PackageComparer(IFileSystem2 fileSystem, Logging.ILogger logger) {
            this.fileSystem = fileSystem;
            this.logger = logger;
        }

        public bool GetChanges(string packageName, string folder) {
            bool packageExists = true;

            IEnumerable<string> currentContents = fileSystem.GetFiles(folder, "*", true).ToList();

            packageExists = DownloadPackage(packageName);

            if (packageExists) {
                var existingPackageDirectory = Path.Combine(folder, "packages", packageName);
                

                var diff = new PackageDifference(fileSystem, logger);
                diff.GetChanges(currentContents, fileSystem.GetFiles(existingPackageDirectory, "*", true));
            } else {
                return true;
            }

            return false;
        }

        private bool DownloadPackage(string packageName) {
            // Download the existing package
            try {
                using (PackageManager packageManager = new PackageManager(fileSystem, logger)) {
                    packageManager.Add(new DependencyFetchContext(false), new[] {
                        new ExpertModule {
                            Name = packageName,
                            GetAction = GetAction.NuGet
                        }
                    });
                    packageManager.Restore();
                }
            } catch (Exception ex) {
                if (ex.Message.Contains("Could not find versions for package")) {
                    logger.Warning("Package {0} doesn't exist. Assuming new.", packageName);

                    return false;
                }
            }
            return true;
        }
    }
}