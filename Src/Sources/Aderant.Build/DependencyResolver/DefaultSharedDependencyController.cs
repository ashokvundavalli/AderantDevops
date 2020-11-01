﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyResolver {
    internal class DefaultSharedDependencyController {
        private readonly IFileSystem fileSystem;

        private readonly string[] reservedDirectories = new string[] {
            "Build"
        };

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

                if (reservedDirectories.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase)) {
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
