using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.TeamFoundation;
using Aderant.Build.VersionControl;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly IBucketService bucketService;
        private readonly IFileSystem fileSystem;

        public ArtifactService() : this(new PhysicalFileSystem(), new BucketService()) {
        }

        public ArtifactService(IFileSystem fileSystem, IBucketService bucketService) {
            this.fileSystem = fileSystem;
            this.bucketService = bucketService;
        }

        public string FileVersion { get; set; }
        public string AssemblyVersion { get; set; }
        public VsoBuildCommands VsoCommands { get; set; }

        internal IReadOnlyCollection<BuildArtifact> PublishArtifacts(BuildOperationContext context, IReadOnlyCollection<ArtifactPackage> packages) {
            var results = PublishInternal(context, packages);

            if (VsoCommands != null) {
                foreach (var item in results) {
                    VsoCommands.LinkArtifact(item.Name, VsoBuildArtifactType.FilePath, item.ComputeVsoPath());
                }
            }

            return results;
        }

        private IReadOnlyCollection<BuildArtifact> PublishInternal(BuildOperationContext context, IReadOnlyCollection<ArtifactPackage> packages) {
            // TODO: Slow - optimize
            // TODO: Test for duplicates in the artifact inputs

            List<Tuple<string, PathSpec>> paths = new List<Tuple<string, PathSpec>>();
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();

            IEnumerable<IGrouping<string, ArtifactPackage>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                foreach (var artifact in group) {
                    var files = artifact.GetFiles();

                    buildArtifacts.Add(PrepareFilesForDrop(paths, context, artifact.Id, files));

                    if (context.BuildMetadata.IsPullRequest) {
                        buildArtifacts.Add(PrepareFilesForPullRequestDrop(paths, context, artifact.Id, files));
                    } else {
                        buildArtifacts.Add(PrepareFilesForLegacyDrop(paths, context, artifact.Id, files));
                    }
                }
            }

            foreach (var item in paths) {
                CopyToDestination(item.Item1, item.Item2);
            }

            return buildArtifacts;
        }

        private BuildArtifact PrepareFilesForDrop(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            BucketId bucket = context.SourceTreeMetadata.GetBucket(BucketId.Current);
            var container = Path.Combine(context.PrimaryDropLocation, bucket.Id, context.BuildMetadata.BuildId.ToString(CultureInfo.InvariantCulture), artifactId);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(container, pathSpec));
            }

            return new BuildArtifact {
                FullPath = container,
                Name = artifactId,
            };
        }

        private BuildArtifact PrepareFilesForPullRequestDrop(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
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
                copyList.Add(
                    Tuple.Create(
                        Path.GetFullPath(Path.Combine(destination, @"..\..\", PathSpec.BuildSucceeded.Location)) /* Assumes the destination is Bin\Module */,
                        PathSpec.BuildSucceeded));
            }

            return new BuildArtifact {
                FullPath = destination,
                Name = artifactName,
            };
        }

        private string CreateDropLocationPath(string destinationRoot, string artifactId, out string artifactName) {
            artifactName = Path.Combine(artifactId, AssemblyVersion, FileVersion);
            return Path.GetFullPath(Path.Combine(destinationRoot, artifactName, "Bin", "Module")); //TODO: Bin\Module is for compatibility with FBDS 
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

            var destination = Path.Combine(destinationRoot, pathSpec.Destination ?? string.Empty);

            if (fileSystem.FileExists(pathSpec.Location)) {
                fileSystem.CopyFile(pathSpec.Location, destination);
            }
        }

        private BuildArtifact PrepareFilesForLegacyDrop(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            string artifactName;
            var destination = CreateDropLocationPath(context.PrimaryDropLocation, artifactId, out artifactName);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(destination, pathSpec));
            }

            return new BuildArtifact {
                FullPath = destination,
                Name = artifactName,
            };
        }

        public void Resolve(BuildOperationContext context, DependencyManifest manifest, string artifactDirectory, IReadOnlyCollection<ArtifactPackage> artifacts) {

            foreach (var artifact in artifacts) {

            }

         
        }
    }
}
