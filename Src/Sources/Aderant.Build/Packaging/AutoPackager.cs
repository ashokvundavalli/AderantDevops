using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {
    internal class AutoPackager {
        private readonly ILogger logger;

        public AutoPackager(ILogger logger) {
            this.logger = logger;
        }

        public IReadOnlyCollection<PathSpec> BuildArtifact(IReadOnlyCollection<PathSpec> filesToPackage, IEnumerable<ProjectOutputSnapshot> outputs) {
            var artifactItems = new List<PathSpec>();
            if (filesToPackage.Count == 0) {
                return artifactItems;
            }

            List<string> filesProducedByProjects = new List<string>();

            foreach (var project in outputs) {
                string outputPath = project.OutputPath;

                // Normalize path as sometimes it ends with two slashes
                string projectOutputPath = outputPath.NormalizeTrailingSlashes();

                if (outputPath != projectOutputPath) {
                    logger.Warning($"! Project {project.ProjectFile} output path ends with two path separators: '{projectOutputPath}'. Normalize this path.");
                }

                foreach (var file in project.FilesWritten) {
                    string outputRelativePath = null;

                    var pos = file.IndexOf(projectOutputPath, StringComparison.OrdinalIgnoreCase);
                    if (pos >= 0) {
                        outputRelativePath = file.Remove(pos, projectOutputPath.Length);
                    }

                    if (outputRelativePath != null && !Path.IsPathRooted(outputRelativePath)) {
                        if (!filesProducedByProjects.Contains(outputRelativePath)) {
                            filesProducedByProjects.Add(outputRelativePath);
                        }
                    }
                }
            }

            var packageQueue = filesToPackage.ToList();

            for (var i = packageQueue.Count - 1; i >= 0; i--) {
                var file = packageQueue[i];

                foreach (var output in filesProducedByProjects) {
                    if (string.Equals(file.Destination, output, StringComparison.OrdinalIgnoreCase)) {

                        if (!artifactItems.Contains(file)) {
                            logger.Info(file.Location);
                            artifactItems.Add(file);

                            packageQueue.RemoveAt(i);
                        }
                    }
                }
            }

            for (var i = packageQueue.Count - 1; i >= 0; i--) {
                var file = packageQueue[i];

                if (PackageOtherExtensions(file)) {
                    artifactItems.Add(file);
                    packageQueue.RemoveAt(i);
                }
            }

            foreach (var item in packageQueue) {
                logger.Warning("File was not packaged: " + item.Location);
            }

            return artifactItems;
        }

        private static bool PackageOtherExtensions(PathSpec file) {
            foreach (var extension in new[] { ".zip", ".msi" }) {
                if (file.Location.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<ArtifactPackageDefinition> CreatePackages(IEnumerable<ProjectOutputSnapshot> snapshots, IEnumerable<ArtifactPackageDefinition> packages, IEnumerable<ArtifactPackageDefinition> autoPackages) {
            foreach (var artifactPackageDefinition in autoPackages) {
                if (packages.Any(s => string.Equals(s.Id, artifactPackageDefinition.Id, StringComparison.OrdinalIgnoreCase))) {
                    throw new InvalidOperationException("A generated package cannot have the same name as a custom package. The package name is: " + artifactPackageDefinition.Id);
                }
            }

            var allFiles = packages.SelectMany(s => s.GetFiles()).ToList();

            foreach (var autoPackage in autoPackages.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)) {
                var packageContent = FilterGeneratedPackage(snapshots, autoPackage, allFiles);

                allFiles.AddRange(packageContent);

                if (packageContent.Count > 0) {
                    yield return new ArtifactPackageDefinition(autoPackage.Id, packageContent) { IsAutomaticallyGenerated = true };
                }
            }
        }

        private IReadOnlyCollection<PathSpec> FilterGeneratedPackage(IEnumerable<ProjectOutputSnapshot> snapshot, ArtifactPackageDefinition definition, List<PathSpec> allFiles) {
            logger.Info("Building package: " + definition.Id);

            var filesFromDefinition = definition.GetFiles().ToList();

            var uniqueContent = new List<PathSpec>();

            foreach (var path in filesFromDefinition) {
                bool add = true;

                foreach (PathSpec spec in allFiles) {
                    if (path == spec) {
                        add = false;
                        // Another package already defines this path combination
                        break;
                    }

                    // If two files are going to identical destinations then this is a double write
                    // and cannot be allowed to ensure deterministic behaviour
                    if (string.Equals(path.Destination, spec.Destination, StringComparison.OrdinalIgnoreCase)) {
                        add = false;
                        // Another package already defines this path combination
                        break;
                    }
                }

                if (add) {
                    uniqueContent.Add(path);
                }
            }

            bool isTestPackage = definition.IsAutomaticallyGenerated && definition.IsTestPackage;

            return BuildArtifact(uniqueContent, snapshot);
        }
    }
}