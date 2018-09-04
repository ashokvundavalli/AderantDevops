using System.Globalization;
using System.IO;
using Aderant.Build.VersionControl;

namespace Aderant.Build {
    internal class ArtifactStagingPathBuilder {
        private readonly int buildId;
        private readonly SourceTreeMetadata metadata;

        public ArtifactStagingPathBuilder(string artifactStagingDirectory, int buildId, SourceTreeMetadata metadata) {
            this.buildId = buildId;
            this.metadata = metadata;

            ErrorUtilities.IsNotNull(artifactStagingDirectory, nameof(artifactStagingDirectory));

            this.StagingDirectory = Path.Combine(artifactStagingDirectory, "_artifacts");
        }

        public string StagingDirectory { get; }


        /// <summary>
        /// Gets a versioned path
        /// </summary>
        public string GetBucketInstancePath(string name) {
            BucketId bucket = metadata.GetBucket(name);

            return Path.Combine(
                StagingDirectory,
                bucket != null ? bucket.Id ?? string.Empty : string.Empty,
                buildId.ToString(CultureInfo.InvariantCulture));

        }
    }
}
