using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly BucketService bucketService;
        private readonly IFileSystem fileSystem;

        public ArtifactService(IFileSystem fileSystem, BucketService bucketService) {
            this.fileSystem = fileSystem;
            this.bucketService = bucketService;
        }

        public string FileVersion { get; set; }
        public string AssemblyVersion { get; set; }

        internal IReadOnlyCollection<ArtifactStorageInfo> PublishArtifacts(Context context, string solutionRoot, IReadOnlyCollection<ArtifactPackage> packages) {
            // TODO: Slow - optimize
            // TODO: Test for duplicates in the artifact inputs

            List<Tuple<string, PathSpec>> copyList = new List<Tuple<string, PathSpec>>();
            List<ArtifactStorageInfo> storageInfoList = new List<ArtifactStorageInfo>();

            IEnumerable<IGrouping<string, ArtifactPackage>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                string bucketId = bucketService.GetBucketId(solutionRoot);
                // \\dfs\artifacts\<name>\<bucket>\<build_id>
                foreach (var artifact in group) {
                    var container = Path.Combine(context.PrimaryDropLocation, group.Key, bucketId, !string.IsNullOrEmpty(context.BuildMetadata.BuildId) ? context.BuildMetadata.BuildId : "-1");
                    foreach (var pathSpec in artifact.GetFiles()) {
                        //CopyToDestination(container, pathSpec);
                    }
                    
                    if (context.BuildMetadata.IsPullRequest) {
                        storageInfoList.Add(PrepareFilesForPullRequestDrop(copyList, context, artifact.Id, artifact.GetFiles()));
                    } else {
                        storageInfoList.Add(PrepareFilesForLegacyDrop(copyList, context, artifact.Id, artifact.GetFiles()));
                    }
                }
            }

            foreach (var item in copyList) {
                
                    CopyToDestination(item.Item1, item.Item2);
              
            }

            return storageInfoList;
        }

        private ArtifactStorageInfo PrepareFilesForPullRequestDrop(List<Tuple<string, PathSpec>> copyList, Context context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            ErrorUtilities.IsNotNull(context.PullRequestDropLocation, nameof(context.PullRequestDropLocation));
            ErrorUtilities.IsNotNull(context.BuildMetadata.PullRequest.Id, nameof(context.BuildMetadata.PullRequest.Id));
            ErrorUtilities.IsNotNull(artifactId, nameof(artifactId));
            ErrorUtilities.IsNotNull(AssemblyVersion, nameof(AssemblyVersion));
            ErrorUtilities.IsNotNull(FileVersion, nameof(FileVersion));

            // TODO: Temporary shim to allow PR layering to work. This should be replaced by the artifact service
            var destination = Path.Combine(context.PullRequestDropLocation, context.BuildMetadata.PullRequest.Id);
            string artifactName;
            destination = CreateDropLocationPath(destination, artifactId, out artifactName);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(destination, pathSpec));
            }

            // Add marker file
            if (destination.EndsWith("Bin\\Module", StringComparison.OrdinalIgnoreCase)) {
                copyList.Add(Tuple.Create(Path.GetFullPath(Path.Combine(destination, @"..\", PathSpec.BuildSucceeded.Location)) /* Assumes the destination is Bin\Module */, PathSpec.BuildSucceeded));
            }

            return new ArtifactStorageInfo {
                FullPath = destination,
                Name = artifactName,
            };
        }

        private string CreateDropLocationPath(string destinationRoot, string artifactId, out string artifactName) {
            artifactName = Path.Combine(artifactId, AssemblyVersion, FileVersion); 
            return Path.Combine(destinationRoot, Path.Combine(artifactName, "Bin", "Module")); //TODO: Bin\Module is for compatibility with FBDS 
        }

        private void CopyToDestination(string destinationRoot, PathSpec pathSpec) {
            if (pathSpec.Location == PathSpec.BuildSucceeded.Location) {
                try {
                    fileSystem.AddFile(destinationRoot, new MemoryStream());
                } catch (IOException) {
                    throw new IOException("Failed to create new file at " + destinationRoot);
                 }

            return;
            }

            var destination = Path.Combine(destinationRoot, pathSpec.Destination);

            if (fileSystem.FileExists(pathSpec.Location)) {
                fileSystem.CopyFile(pathSpec.Location, destination);
            }
        }

        private ArtifactStorageInfo PrepareFilesForLegacyDrop(List<Tuple<string, PathSpec>> copyList, Context context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            string artifactName;
            var destination = CreateDropLocationPath(context.PrimaryDropLocation, artifactId, out artifactName);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(destination, pathSpec));
            }

            return new ArtifactStorageInfo {
                FullPath = destination,
                Name = artifactName,
            };
        }
    }
}
