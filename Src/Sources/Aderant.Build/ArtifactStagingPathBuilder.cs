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
        /// Gets a location to store the artifact. If the item is not known the source control system it returns null.
        /// </summary>
        public string CreatePath(string name, out bool sendToArtifactCache) {
            BucketId bucket = metadata.GetBucket(name);

            sendToArtifactCache = bucket != null && !string.IsNullOrEmpty(bucket.DirectorySegment);

            string directorySegment;
            if (!string.IsNullOrWhiteSpace(bucket?.DirectorySegment)) {
                directorySegment = bucket.DirectorySegment;
            } else {
                directorySegment = name;
            }

            return Path.Combine(StagingDirectory, directorySegment, buildId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
