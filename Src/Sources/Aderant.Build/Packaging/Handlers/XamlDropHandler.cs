﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.Packaging.Handlers {
    internal class XamlDropHandler :  IArtifactHandler {
        public string FileVersion { get; set; }
        public string AssemblyVersion { get; set; }

        public XamlDropHandler(string fileVersion, string assemblyVersion) {
            FileVersion = fileVersion;
            AssemblyVersion = assemblyVersion;
        }

        public BuildArtifact ProcessFiles(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            string artifactName;
            var destination = CreateDropLocationPath(context.ArtifactStagingDirectory, artifactId, out artifactName);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(destination, pathSpec));
            }

            return new BuildArtifact {
                FullPath = destination,
                Name = artifactName,
            };
        }

        private string CreateDropLocationPath(string destinationRoot, string artifactId, out string artifactName) {
            artifactName = Path.Combine(artifactId, AssemblyVersion, FileVersion);
            return Path.GetFullPath(Path.Combine(destinationRoot, artifactName, "Bin", "Module")); //TODO: Bin\Module is for compatibility with FBDS 
        }
    }
}
