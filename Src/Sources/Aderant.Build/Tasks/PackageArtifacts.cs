using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;
using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class PublishArtifacts : ContextTaskBase {

        public string SolutionRoot { get; set; }

        public string[] RootingPaths { get; set; }

        public ITaskItem[] Artifacts { get; set; }

        public override bool Execute() {
            if (Artifacts != null) {
                var artifacts = CreateArtifactPackagesFromTaskItems();
                var artifactService = new ArtifactService(new PhysicalFileSystem(), new BucketService());
                artifactService.PublishArtifacts(Context, SolutionRoot, null, artifacts);
            }

            return !Log.HasLoggedErrors;
        }

        private List<ArtifactPackage> CreateArtifactPackagesFromTaskItems() {
            List<ArtifactPackage> artifacts = new List<ArtifactPackage>();
            var grouping = Artifacts.GroupBy(g => g.GetMetadata("ArtifactId"));

            foreach (var group in grouping) {
                List<PathSpec> pathSpecs = new List<PathSpec>();
                foreach (var file in group) {
                    var pathSpec = ArtifactPackage.CreatePathSpecification(
                        SolutionRoot,
                        RootingPaths,
                        file.GetMetadata("FullPath"),
                        file.GetMetadata("TargetPath") // The destination relative location
                    );

                    if (!pathSpecs.Contains(pathSpec)) {
                        pathSpecs.Add(pathSpec);
                    }
                }

                var artifact = new ArtifactPackage(group.Key, pathSpecs);
                artifacts.Add(artifact);
            }

            return artifacts;
        }
    }

    internal class ArtifactService {
        private readonly IFileSystem fileSystem;
        private readonly BucketService bucketService;

        public ArtifactService(IFileSystem fileSystem, BucketService bucketService) {
            this.fileSystem = fileSystem;
            this.bucketService = bucketService;
        }

        internal void PublishArtifacts(Context context, string solutionRoot, object cacheKey, IReadOnlyCollection<ArtifactPackage> packages) {
            // TODO: Slow - optimize
            IEnumerable<IGrouping<string, ArtifactPackage>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                string bucketId = bucketService.GetBucketId(solutionRoot);
                // \\dfs\artifacts\<name>\<bucket>\<build_id>
                foreach (var artifact in group) {

                    foreach (var pathSpec in artifact.GetFiles()) {
                        var container = Path.Combine(context.PrimaryDropLocation, group.Key, bucketId, !string.IsNullOrEmpty(context.BuildMetadata.BuildId) ? context.BuildMetadata.BuildId : "-1");

                        var destination = Path.Combine(container, pathSpec.Destination);

                        if (fileSystem.FileExists(pathSpec.FullPath)) {
                            fileSystem.CopyFile(pathSpec.FullPath, destination);
                        }
                    }
                }
            }
        }
    }

    internal class BucketService {
        public string GetBucketId(string path) {
            return GetCommitForSolutionRoot(path);
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

    internal class ArtifactPackage : IArtifact {
        private List<PathSpec> pathSpecs;

        public ArtifactPackage(string id, IEnumerable<PathSpec> pathSpecs) {
            Id = id;
            this.pathSpecs = pathSpecs.ToList();
        }

        public string Id { get; }

        public IReadOnlyCollection<IDependable> GetDependencies() {
            return null;
        }

        public void AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
        }

        public IReadOnlyCollection<PathSpec> GetFiles() {
            return pathSpecs;
        }

        public static PathSpec CreatePathSpecification(string solutionRoot, string[] trimPaths, string fullPath, string targetPath) {
            string outputRelativePath = null;

            foreach (var path in trimPaths) {
                if (fullPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
                    outputRelativePath = fullPath.Remove(0, path.Length);
                    break;
                }
            }

            if (outputRelativePath == null) {
                throw new InvalidOperationException(string.Format("Unable to construct an output relative path from {0} to {1}", solutionRoot, fullPath));
            }

            return new PathSpec(fullPath, Path.Combine(targetPath ?? string.Empty, outputRelativePath));
        }
    }

    internal struct PathSpec {
        private readonly string fullPath;
        private readonly string relativePath;

        public PathSpec(string fullPath, string relativePath) {
            this.fullPath = fullPath;
            this.relativePath = relativePath;
        }

        public string Destination {
            get { return relativePath; }
        }

        public string FullPath {
            get { return fullPath; }
        }

        public override bool Equals(object obj) {
            if (!(obj is PathSpec)) {
                return false;
            }

            var spec = (PathSpec)obj;
            return fullPath == spec.fullPath && relativePath == spec.relativePath;
        }

        public override int GetHashCode() {
            var hashCode = -79747215;
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(fullPath);
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(relativePath);
            return hashCode;
        }
    }

}
