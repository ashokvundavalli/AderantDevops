using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.Packaging {
    internal class TestPackageBuilder {
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
    }
}
