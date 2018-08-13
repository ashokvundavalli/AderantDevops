using System.Globalization;
using System.IO;
using Aderant.Build.VersionControl;
using LibGit2Sharp;

namespace Aderant.Build.Packaging {
    internal class BucketService : IBucketService {
        public virtual string GetBucketId(string path) {
            string discover = Repository.Discover(path);

            using (var repo = new Repository(discover)) {
                string tipSha = repo.Head.Tip.Sha;

                return tipSha;
            }
        }

        public static string BuildDropLocation(BuildOperationContext buildOperationContext) {
            if (buildOperationContext.SourceTreeMetadata != null) {
                BucketId bucket = buildOperationContext.SourceTreeMetadata.GetBucket(BucketId.Current);

                return Path.Combine(
                    buildOperationContext.PrimaryDropLocation,
                    bucket.Id,
                    buildOperationContext.BuildMetadata.BuildId.ToString(CultureInfo.InvariantCulture));
            }

            return null;
        }
    }

    internal interface IBucketService {
        string GetBucketId(string path);
    }
}
