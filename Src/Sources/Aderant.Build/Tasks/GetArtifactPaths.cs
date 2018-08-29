using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GetArtifactPaths : BuildOperationContextTask {
        private List<BuildArtifact> developmentArtifacts;
        private List<BuildArtifact> packagePaths;
        private List<BuildArtifact> testPackages;

        public bool IncludeGeneratedArtifacts { get; set; }

        [Output]
        public string[] ArtifactPaths {
            get { return packagePaths.Select(s => s.FullPath).ToArray(); }
        }

        [Output]
        public string[] DevelopmentPackagePaths {
            get { return developmentArtifacts.Select(s => s.FullPath).ToArray(); }
        }

        [Output]
        public string[] TestPackagePaths {
            get { return testPackages.Select(s => s.FullPath).ToArray(); }
        }

        public override bool ExecuteTask() {
            BuildArtifact[] associatedArtifacts = PipelineService.GetAssociatedArtifacts();

            this.packagePaths = new List<BuildArtifact>();
            this.developmentArtifacts = new List<BuildArtifact>();
            this.testPackages = new List<BuildArtifact>();

            foreach (BuildArtifact artifact in associatedArtifacts) {
                if (artifact.IsInternalDevelopmentPackage) {
                    Log.LogMessage(MessageImportance.Normal, "Development package: " + artifact.Name);

                    developmentArtifacts.Add(artifact);
                    continue;
                }

                if (artifact.IsTestPackage) {
                    Log.LogMessage(MessageImportance.Normal, "Test package: " + artifact.Name);
                    testPackages.Add(artifact);
                    continue;
                }

                if (IncludeGeneratedArtifacts) {
                    Log.LogMessage(MessageImportance.Normal, "Generated package: " + artifact.Name);
                    packagePaths.Add(artifact);
                } else {
                    if (!artifact.IsAutomaticallyGenerated) {
                        packagePaths.Add(artifact);
                    } else {
                        Log.LogMessage(MessageImportance.Normal, "Excluding package: " + artifact.Name);
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }

}
