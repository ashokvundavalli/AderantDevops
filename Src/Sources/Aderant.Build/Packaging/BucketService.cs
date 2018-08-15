using System.Globalization;
using System.IO;
using Aderant.Build.VersionControl;
using LibGit2Sharp;

namespace Aderant.Build.Packaging {
    internal class BucketPathBuilder : IBucketPathBuilder {
        public virtual string GetBucketId(string path) {
            string discover = Repository.Discover(path);

            using (var repo = new Repository(discover)) {
                string tipSha = repo.Head.Tip.Sha;

                return tipSha;
            }
        }

        public static string BuildDropLocation(string name, BuildOperationContext buildOperationContext) {
            if (buildOperationContext.SourceTreeMetadata != null) {
                BucketId bucket = buildOperationContext.SourceTreeMetadata.GetBucket(name);

                return Path.Combine(
                    buildOperationContext.PrimaryDropLocation,
                    bucket != null ? bucket.Id ?? string.Empty : string.Empty,
                    buildOperationContext.BuildMetadata.BuildId.ToString(CultureInfo.InvariantCulture));
            }

            return null;
        }
    }

    internal interface IBucketPathBuilder {
        string GetBucketId(string path);
    }
}
