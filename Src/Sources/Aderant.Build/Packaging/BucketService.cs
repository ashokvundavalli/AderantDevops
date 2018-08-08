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

    }

    internal interface IBucketService {
        string GetBucketId(string path);
    }
}
