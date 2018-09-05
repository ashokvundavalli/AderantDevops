using System;
using System.Globalization;
using System.IO;

namespace Aderant.Build.Packaging {
    internal class ArtifactDropPathBuilder {

        public string PrimaryDropLocation { get; set; }
        public string PullRequestDropLocation { get; set; }
        public string StagingDirectory { get; internal set; }

        public string CreatePath(string artifactId, BuildMetadata buildMetadata) {
            string[] parts;

            if (buildMetadata.IsPullRequest) {
                parts = new[] {
                    PullRequestDropLocation,
                    "pulls",
                    buildMetadata.PullRequest.Id,
                    artifactId
                };
            } else {
                if (string.IsNullOrWhiteSpace(buildMetadata.ScmBranch)) {
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
