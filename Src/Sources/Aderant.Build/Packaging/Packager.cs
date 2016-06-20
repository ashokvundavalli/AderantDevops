using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.Versioning;

namespace Aderant.Build.Packaging {
    internal class Packager {
        private readonly IFileSystem fileSystem;
        private readonly FileVersionAnalyzer fileVersionAnalyzer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Packager"/> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="fileVersionAnalyzer">The file version inspector.</param>
        public Packager(IFileSystem fileSystem, FileVersionAnalyzer fileVersionAnalyzer) {
            this.fileSystem = fileSystem;
            this.fileVersionAnalyzer = fileVersionAnalyzer;
        }

        public void CreatePackage(string packageName, string contentDirectory, string outputDirectory) {
            IEnumerable<string> sourceFiles = fileSystem.GetFiles(contentDirectory, "*", true);

            List<PhysicalPackageFile> files = new List<PhysicalPackageFile>();

            foreach (string sourceFile in sourceFiles) {
                // We don't want to add entries for directories
                if (fileSystem.DirectoryExists(sourceFile)) {
                    continue;
                }

                FileVersionDescriptor fileVersionDescriptor = fileVersionAnalyzer.GetVersion(sourceFile);

                PhysicalPackageFile file = new PhysicalPackageFile(sourceFile);
                file.Version = fileVersionDescriptor;
                file.OpenFile = path => fileSystem.OpenFile(path);

                files.Add(file);
            }

            if (files.Count > 0) {
                SemanticVersion packageVersion = CalculatePackageVersion(files);

                Package package = new Package(packageName, new PackageSpecification(packageVersion, files));

                fileSystem.CreateDirectory(outputDirectory);
            
                string file = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.zip", Path.Combine(outputDirectory, packageVersion.ToString(), packageName), packageVersion);

                using (Stream packageStream = fileSystem.CreateFile(file)) {
                    package.CreatePackageFile(contentDirectory, packageStream);
                }
            }
        }

        private SemanticVersion CalculatePackageVersion(List<PhysicalPackageFile> files) {
            PhysicalPackageFile[] orderedFiles = files
                .Where(s => s.Version != null)
                .OrderByDescending(s => s.Version.GetSemanticVersion())
                .ToArray();

            if (orderedFiles.Length > 0) {
                SemanticVersion packageVersion = orderedFiles[0].Version.GetSemanticVersion();

                if (packageVersion == null) {
                    return new SemanticVersion(new Version(1, 0, 0));
                }

                return packageVersion;
            }

            return new SemanticVersion(new Version(1, 0, 0));
        }
    }
}