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

        public IReadOnlyCollection<PathSpec> BuildArtifact(IReadOnlyCollection<PathSpec> filesToPackage, IEnumerable<OutputFilesSnapshot> outputs, string publisherName) {
            List<string> outputList = new List<string>();

            foreach (var project in outputs) {
                if (project.IsTestProject) {
                    foreach (var file in project.FilesWritten) {
                        string outputRelativePath = file.Replace(project.OutputPath, "", StringComparison.OrdinalIgnoreCase);

                        if (!Path.IsPathRooted(outputRelativePath)) {
                            if (!outputList.Contains(outputRelativePath)) {
                                outputList.Add(outputRelativePath);
                            }
                        }
                    }
                }
            }

            var artifactItems = new List<PathSpec>();

            foreach (var file in filesToPackage) {
                foreach (var output in outputList) {
                    if (string.Equals(file.Destination, output, StringComparison.OrdinalIgnoreCase)) {

                        if (!artifactItems.Contains(file)) {
                            logger.Info(file.Location);
                            artifactItems.Add(file);
                        }
                    }
                }
            }

            return artifactItems;
        }

        public IEnumerable<ArtifactPackageDefinition> CreatePackages(IEnumerable<OutputFilesSnapshot> snapshots, string publisherName, IEnumerable<ArtifactPackageDefinition> packages, IEnumerable<ArtifactPackageDefinition> autoPackages) {
            foreach (var artifactPackageDefinition in autoPackages) {
                if (packages.Any(s => s.Id == artifactPackageDefinition.Id)) {
                    throw new InvalidOperationException("A generated package cannot have the same name as a custom package. The package name is: " + artifactPackageDefinition.Id);
                }
            }

            var allFiles = packages.SelectMany(s => s.GetFiles()).ToList();

            foreach (var autoPackage in autoPackages.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)) {
                var packageContent = FilterGeneratedPackage(snapshots, publisherName, autoPackage, allFiles);

                allFiles.AddRange(packageContent);

                if (packageContent.Count > 0) {
                    yield return new ArtifactPackageDefinition(autoPackage.Id, packageContent) { IsAutomaticallyGenerated = true };
                }
            }
        }

        private IReadOnlyCollection<PathSpec> FilterGeneratedPackage(IEnumerable<OutputFilesSnapshot> snapshot, string publisherName, ArtifactPackageDefinition definition, List<PathSpec> artifact) {
            var files = definition.GetFiles().ToList();

            var uniqueContent = new List<PathSpec>();

            foreach (var path in files) {
                bool add = true;
                foreach (PathSpec spec in artifact) {
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

            logger.Info("Building package: " + definition.Id);
            return BuildArtifact(uniqueContent, snapshot, publisherName);
        }
    }
}
