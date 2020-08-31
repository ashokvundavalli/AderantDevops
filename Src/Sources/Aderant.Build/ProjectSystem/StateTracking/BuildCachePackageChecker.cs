using System;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.Logging;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class BuildCachePackageChecker {
        private readonly ILogger logger;

        public BuildCachePackageChecker(ILogger logger) {
            this.logger = logger;
        }

        public ICollection<ArtifactManifest> Artifacts { get; set; }

        public bool DoesArtifactContainProjectItem(ConfiguredProject project) {
            ErrorUtilities.IsNotNull(Artifacts, nameof(Artifacts));

            // Packaged files such as workflows and web projects produce both an assembly an a package
            // We want to interrogate the package for the packaged content if we have one of those projects
            var outputFile = project.GetOutputAssemblyWithExtension();
            var fileName = outputFile;

            if (project.IsZipPackaged) {
                outputFile = Path.ChangeExtension(outputFile, ".zip");
            }

            outputFile = Path.Combine(project.OutputPath, outputFile);

            List<ArtifactManifest> checkedArtifacts = null;

            foreach (ArtifactManifest artifactManifest in Artifacts) {
                foreach (ArtifactItem file in artifactManifest.Files) {
                    if (outputFile.IndexOf(file.File, StringComparison.OrdinalIgnoreCase) >= 0) {
                        return true;
                    }
                }

                if (checkedArtifacts == null) {
                    checkedArtifacts = new List<ArtifactManifest>();
                }

                checkedArtifacts.Add(artifactManifest);
            }

            if (checkedArtifacts != null && checkedArtifacts.Count > 0) {
                logger.Info($"Looked for {fileName} but it was not found in packages:");

                foreach (var checkedArtifact in checkedArtifacts) {
                    logger.Info(string.Format("    {0} (package id: {1})", checkedArtifact.Id.PadRight(80), checkedArtifact.InstanceId));
                }
            }
      
            return false;
        }
    }
}