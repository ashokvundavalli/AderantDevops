﻿using System;
using System.Globalization;
using System.IO;

namespace Aderant.Build.Packaging {
    internal class ArtifactDropPathBuilder {

        public string PrimaryDropLocation { get; set; }
        public string PullRequestDropLocation { get; set; }
        public string StagingDirectory { get; internal set; }

        public string CreatePath(string artifactId, BuildMetadata buildMetadata) {
            return CreatePath(artifactId, buildMetadata, false);
        }

        public string CreatePath(string artifactId, BuildMetadata buildMetadata, bool allowNullScmBranch) {
            string[] parts;

            if (buildMetadata.IsPullRequest) {
                parts = new[] {
                    PullRequestDropLocation,
                    buildMetadata.PullRequest.Id,
                    artifactId
                };
            } else {
                if (!allowNullScmBranch && string.IsNullOrWhiteSpace(buildMetadata.ScmBranch)) {
                    var invalidOperation = new InvalidOperationException("When constructing a drop path ScmBranch cannot be null or empty.");
                    throw invalidOperation;
                }

                parts = new[] {
                    PrimaryDropLocation,
                    buildMetadata.ScmBranch.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar /*UNIX/git paths fix up to make them Windows paths*/),
                    buildMetadata.BuildId.ToString(CultureInfo.InvariantCulture),
                    artifactId
                };
            }

            return Path.Combine(parts);
        }
    }
}
