using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aderant.Build.Packaging {
    internal class AutoPackager {
        public IReadOnlyCollection<PathSpec> BuildArtifact(IReadOnlyCollection<PathSpec> filesToPackage, ProjectOutputSnapshot outputs, string publisherName) {
            var set = outputs.GetProjectsForTag(publisherName);

            List<string> outputList = new List<string>();

            foreach (var project in set.Values) {

                if (project.IsTestProject) {
                    foreach (var path in project.FilesWritten) {

                        var name = Path.GetFileName(path);

                        if (!outputList.Contains(name)) {
                            outputList.Add(name);
                        }
                    }
                }
            }

            var artifactItems = new List<PathSpec>();

            foreach (var file in filesToPackage) {
                var fileName = Path.GetFileName(file.Location);

                foreach (var output in outputList) {
                    if (string.Equals(fileName, output, StringComparison.OrdinalIgnoreCase)) {
                        artifactItems.Add(file);
                    }
                }
            }

            return artifactItems;
        }

        public IEnumerable<ArtifactPackageDefinition> CreatePackages(ProjectOutputSnapshot snapshot, string publisherName, IEnumerable<ArtifactPackageDefinition> packages, IEnumerable<ArtifactPackageDefinition> autoPackages) {
            foreach (var artifactPackageDefinition in autoPackages) {
                if (packages.Any(s => s.Id == artifactPackageDefinition.Id)) {
                    throw new InvalidOperationException("A generated package cannot have the same name as a custom package. The package name is: " + artifactPackageDefinition.Id);
                }
            }

            var allFiles = packages.SelectMany(s => s.GetFiles()).ToList();
            
            foreach (var autoPackage in autoPackages.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)) {
                var packageContent = FilterGeneratedPackage(snapshot, publisherName, autoPackage, allFiles);
                
                allFiles.AddRange(packageContent);

                yield return new ArtifactPackageDefinition(autoPackage.Id, packageContent);
            }
        }

        private IReadOnlyCollection<PathSpec> FilterGeneratedPackage(ProjectOutputSnapshot snapshot, string publisherName, ArtifactPackageDefinition filesToPackage, List<PathSpec> artifact) {
            var files = filesToPackage.GetFiles().ToList();

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

            return BuildArtifact(uniqueContent, snapshot, publisherName);
        }
    }
}
