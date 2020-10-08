using System.Collections.Generic;
using System.IO;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyResolver {
    internal class DefaultSharedDependencyController {
        private readonly IFileSystem fileSystem;

        public DefaultSharedDependencyController(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        public void CreateLinks(string rootDirectory, IEnumerable<string> directoriesInBuild) {
            rootDirectory = rootDirectory.TrimTrailingSlashes();

            var map = new[] {
                new {
                    LinkPath = "packages",
                    LinkTarget = Path.Combine(rootDirectory, "packages"),
                },
                new {
                    LinkPath = "paket-files",
                    LinkTarget = Path.Combine(rootDirectory, "paket-files"),
                },
                new {
                    LinkPath = "Dependencies",
                    LinkTarget = rootDirectory,
                }
            };

            foreach (var directory in directoriesInBuild) {
                if (PathComparer.Default.Equals(rootDirectory, directory)) {
                    continue;
                }

                foreach (var link in map) {
                    string linkPath = Path.Combine(directory, link.LinkPath);
                    fileSystem.CreateDirectory(link.LinkTarget);
                    fileSystem.CreateDirectoryLink(linkPath, link.LinkTarget, true);
                }
            }
        }
    }
}
