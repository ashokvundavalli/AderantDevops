using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Aderant.Build.Logging;
using Aderant.Build.Versioning;

namespace Aderant.Build.Packaging.NuGet {
    internal class PackageComparer {
        private readonly FileVersionAnalyzer fileVersionAnalyzer;
        private readonly IFileSystem2 fileSystem;
        private readonly Logging.ILogger logger;

        public PackageComparer(IFileSystem2 fileSystem, ILogger logger) : this(fileSystem, logger, new FileVersionAnalyzer()) {
        }

        private PackageComparer(IFileSystem2 fileSystem, ILogger logger, FileVersionAnalyzer fileVersionAnalyzer) {
            this.fileSystem = fileSystem;
            this.fileVersionAnalyzer = fileVersionAnalyzer;
            this.fileVersionAnalyzer.OpenFile = fileSystem.OpenFile;

            this.logger = logger;
        }

        /// <summary>
        /// Determines if the latest package is different that in the source tree.
        /// </summary>
        public bool HasChanges(string existingPackageDirectory, string currentPackageDirectory, string customNuspecFile) {
            if (!currentPackageDirectory.EndsWith("bin")) {
                currentPackageDirectory = Path.Combine(currentPackageDirectory, "bin");
            }

            logger.Info("Existing package directory: {0}. Current package directory: {1}", existingPackageDirectory, currentPackageDirectory);

            var currentContents = fileSystem.GetFiles(currentPackageDirectory, "*", true).ToList();
            var existingContents = GetExistingContents(existingPackageDirectory);

            // Full file path is the key, value is the hash
            Dictionary<string, string> currentHashes = HashContents(currentContents);
            Dictionary<string, string> existingHashes = HashContents(existingContents);

            IEnumerable<string> difference = currentHashes.Values.Except(existingHashes.Values, StringComparer.OrdinalIgnoreCase);

            foreach (string fileHash in difference) {
                string filePath = currentHashes.FirstOrDefault(kvp => string.Equals(kvp.Value, fileHash, StringComparison.OrdinalIgnoreCase)).Key;

                FileVersionDescriptor version = null;
                if (!string.IsNullOrEmpty(filePath)) {
                    var packageRelativeFilePath = TrimLeadingPath(currentPackageDirectory, filePath);
                    try {
                        version = fileVersionAnalyzer.GetVersion(filePath);
                    } catch {
                        logger.Warning($"Failed to retrieve version information for: '{filePath}'.");
                    }

                    logger.Info("New or changed item found: {0}. File version: {1}", packageRelativeFilePath, version?.FileVersion ?? string.Empty);
                }
            }

            bool packageFileLayoutChanged = false;

            // We can only do a physical compare if there is not custom packaging as the user may place files into different directories
            if (string.IsNullOrEmpty(customNuspecFile)) {
                // Horrible fudge, it would be better to rename bin to lib in source control so we don't have to hard code this assumption here
                HashSet<string> currentFileLocations = new HashSet<string>(currentContents.Select(x => Path.Combine("lib", TrimLeadingPath(currentPackageDirectory, x))), StringComparer.OrdinalIgnoreCase);
                HashSet<string> existingFileLocations = new HashSet<string>(existingContents.Select(x => TrimLeadingPath(existingPackageDirectory, x)), StringComparer.OrdinalIgnoreCase);

                packageFileLayoutChanged = !currentFileLocations.SetEquals(existingFileLocations);
                if (packageFileLayoutChanged) {
                    logger.Info("Package structure has changed: " + currentPackageDirectory);
                    logger.Info(Environment.NewLine);

                    logger.Info("Current structure:");
                    foreach (var file in currentFileLocations) {
                        logger.Info(file);
                    }

                    logger.Info(Environment.NewLine);

                    logger.Info("Existing structure:");
                    foreach (var file in existingFileLocations) {
                        logger.Info(file);
                    }
                }
            }

            return packageFileLayoutChanged || difference.Any();
        }

        /// <summary>
        /// We do not know the structure of the existing package as it may have a nuspec file with
        /// complicated routing so do a best effort scan of well known nuget folders.
        /// </summary>

        private List<string> GetExistingContents(string existingPackageDirectory) {
            string[] wellKnownFolderNames = new[] { "bin", "lib", "content", "tools", "build" };

            List<string> existingContents = new List<string>();

            foreach (string wellKnownFolderName in wellKnownFolderNames) {
                var pathToScan = Path.Combine(existingPackageDirectory, wellKnownFolderName);

                if (fileSystem.DirectoryExists(pathToScan)) {
                    existingContents.AddRange(fileSystem.GetFiles(pathToScan, "*", true));
                }
            }

            return existingContents;
        }

        /// <summary>
        /// Chops the given leading path segment off so we can compare two folder structures
        /// </summary>
        private static string TrimLeadingPath(string segmentToRemove, string pathWithSegmentAsPrefix) {
            return pathWithSegmentAsPrefix.Replace($@"{segmentToRemove}\", "");
        }

        private Dictionary<string, string> HashContents(IEnumerable<string> files) {
            Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in files) {
                string hashValue;

                using (Stream stream = fileSystem.OpenFile(file)) {
                    using (var sha1 = new SHA1Managed()) {
                        byte[] hash = sha1.ComputeHash(stream);
                        hashValue = BitConverter.ToString(hash);
                    }
                }

                hashes[file] = hashValue;
            }

            return hashes;
        }
    }
}