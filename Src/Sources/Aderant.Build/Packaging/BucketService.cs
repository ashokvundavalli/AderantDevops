using System;
using System.Collections.Generic;
using System.Linq;
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

        private static string GetCommitForSolutionRoot(string solutionRoot) {
            string discover = Repository.Discover(solutionRoot);

            using (var repo = new Repository(discover)) {
                // Covert the full path to the relative path within the repository
                int start = solutionRoot.IndexOf(repo.Info.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
                if (start >= 0) {
                    string relativePath = solutionRoot.Substring(start + repo.Info.WorkingDirectory.Length);

                    IEnumerable<LogEntry> logEntries = repo.Commits.QueryBy(relativePath);
                    var latestCommitForDirectory = logEntries.First();

                    return latestCommitForDirectory.Commit.Id.Sha;
                }

                return string.Empty;
            }
        }
    }

    internal interface IBucketService {
        string GetBucketId(string path);
    }
}
