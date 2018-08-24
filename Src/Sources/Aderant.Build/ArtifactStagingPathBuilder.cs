using System.Globalization;
using System.IO;
using Aderant.Build.VersionControl;

namespace Aderant.Build {
    internal class ArtifactStagingPathBuilder {
        private readonly BuildOperationContext context;
        private string stagingDirectory;

        public ArtifactStagingPathBuilder(BuildOperationContext context) {
            ErrorUtilities.IsNotNull(context, nameof(context));

            this.context = context;
            this.stagingDirectory = Path.Combine(context.ArtifactStagingDirectory, "_artifacts");
        }

        public string StagingDirectory {
            get { return stagingDirectory; }
        }

        public string BuildPath(string name) {
            BucketId bucket = context.SourceTreeMetadata.GetBucket(name);

            return Path.Combine(
                StagingDirectory,
                bucket != null ? bucket.Id ?? string.Empty : string.Empty,
                context.BuildMetadata.BuildId.ToString(CultureInfo.InvariantCulture));

        }
    }
}
