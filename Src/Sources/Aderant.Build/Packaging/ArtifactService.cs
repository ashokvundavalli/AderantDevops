using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Tasks;
using Aderant.Build.TeamFoundation;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly IFileSystem fileSystem;

        public ArtifactService()
            : this(new PhysicalFileSystem()) {
        }

        public ArtifactService(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        public string FileVersion { get; set; }
        public string AssemblyVersion { get; set; }
        public VsoBuildCommands VsoCommands { get; set; }

        /// <summary>
        /// Publishes build artifacts to some kind of repository.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <param name="publisherName">Optional. Used to identify the publisher</param>
        /// <param name="packages">The artifacts to publish</param>
        internal IReadOnlyCollection<BuildArtifact> PublishArtifacts(BuildOperationContext context, string publisherName, IReadOnlyCollection<ArtifactPackage> packages) {
            var results = PublishInternal(context, publisherName, packages);

            if (VsoCommands != null) {
                // Tell TFS about these artifacts
                foreach (var item in results) {
                    VsoCommands.LinkArtifact(item.Name, VsoBuildArtifactType.FilePath, item.ComputeVsoPath());
                }
            }

            return results;
        }

        private IReadOnlyCollection<BuildArtifact> PublishInternal(BuildOperationContext context, string publisherName, IReadOnlyCollection<ArtifactPackage> packages) {
            // TODO: Slow - optimize
            // TODO: Test for duplicates in the artifact inputs

            List<Tuple<string, PathSpec>> paths = new List<Tuple<string, PathSpec>>();
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();

            IEnumerable<IGrouping<string, ArtifactPackage>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                foreach (var artifact in group) {
                    var files = artifact.GetFiles();

                    var filesForDrop = PrepareFilesForDrop(paths, context, artifact.Id, files);
                    if (filesForDrop != null) {
                        buildArtifacts.Add(filesForDrop);
                    }

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

            System.Diagnostics.Debugger.Launch();
            if (!string.IsNullOrWhiteSpace(publisherName)) {
                context.RecordArtifacts(publisherName, packages.Select(s => s.Id));
            }

            return buildArtifacts;
        }

        private BuildArtifact PrepareFilesForDrop(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            var dl = context.GetDropLocation();
            if (dl != null) {
                var container = Path.Combine(dl, artifactId);

                foreach (var pathSpec in files) {
                    copyList.Add(Tuple.Create(container, pathSpec));
                }
                return new BuildArtifact {
                    FullPath = container,
                    Name = artifactId,
                };
            }

            return null;
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

        public ArtifactResolveResult Resolve(BuildOperationContext context, DependencyManifest manifest, IEnumerable<string> artifactsIds) {
            var result = new ArtifactResolveResult();
            List<ArtifactPathSpec> paths = new List<ArtifactPathSpec>();
            result.Paths = paths;

            if (context.StateFile != null) {
                foreach (var artifactId in artifactsIds) {
                    string artifactFolder = Path.Combine(context.StateFile.DropLocation, artifactId);

                    if (fileSystem.DirectoryExists(artifactFolder)) {
                        var spec = new ArtifactPathSpec {
                            ArtifactId = artifactId,
                            Source = artifactFolder
                        };

                        paths.Add(spec);
                    }
                }
            }

            return result;
        }

        public BuildStateMetadata GetBuildStateMetadata(string[] bucketIds, string dropLocation) {
            var metadata = new BuildStateMetadata();
            var files = new List<BuildStateFile>();
            metadata.BuildStateFiles = files;

            foreach (var bucketId in bucketIds) {
                string bucketPath = Path.Combine(dropLocation, bucketId);

                if (fileSystem.DirectoryExists(bucketPath)) {
                    IEnumerable<string> directories = fileSystem.GetDirectories(bucketPath);

                    string[] orderBuildsByBuildNumber = OrderBuildsByBuildNumber(directories.ToArray());

                    foreach (var folder in orderBuildsByBuildNumber) {
                        var stateFile = Path.Combine(folder, BuildStateWriter.DefaultFileName);

                        if (fileSystem.FileExists(stateFile)) {
                            using (Stream stream = fileSystem.OpenFile(stateFile)) {
                                BuildStateFile file = new BuildStateFile().DeserializeCache<BuildStateFile>(stream);
                                file.DropLocation = folder;

                                files.Add(file);
                            }
                        }
                    }
                }
            }

            return metadata;
        }

        internal static string[] OrderBuildsByBuildNumber(string[] entries) {
            List<KeyValuePair<int, string>> numbers = new List<KeyValuePair<int, string>>(entries.Length);

            foreach (var entry in entries) {
                string directoryName = Path.GetFileName(entry);
                int version;
                if (Int32.TryParse(directoryName,  NumberStyles.Any, CultureInfo.InvariantCulture, out version)) {
                    numbers.Add(new KeyValuePair<int, string>(version, entry));
                }
            }

            return numbers.OrderByDescending(d => d.Key).Select(s => s.Value).ToArray();
        }

        
        public void Retrieve(ArtifactResolveResult result, string artifactDirectory, bool flatten) {
            foreach (var spec in result.Paths) {
                string destination;
                if (!flatten) {
                    destination = Path.Combine(artifactDirectory, spec.ArtifactId);
                } else {
                    destination = artifactDirectory;
                }

                fileSystem.CopyDirectory(spec.Source, destination);
            }
        }
    }

    internal class ArtifactPathSpec {
        public string ArtifactId { get; set; }
        public string Source { get; set; }
    }

    internal class ArtifactResolveResult {
        public List<ArtifactPathSpec> Paths { get; set; }
    }

    [Serializable]
    public class BuildStateMetadata {
        public IReadOnlyCollection<BuildStateFile> BuildStateFiles { get; set; }
    }
}
