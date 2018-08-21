using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.TeamFoundation;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        public ArtifactService(ILogger logger)
            : this(logger, new PhysicalFileSystem()) {
        }

        public ArtifactService(ILogger logger, IFileSystem fileSystem) {
            this.logger = logger;
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
            List<Tuple<string, PathSpec>> copyList = new List<Tuple<string, PathSpec>>();
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();

            IEnumerable<IGrouping<string, ArtifactPackage>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                foreach (var artifact in group) {
                    //if (artifact.Id == "Tests.Framework") {
                    //    System.Diagnostics.Debugger.Launch();
                    //}

                    var files = artifact.GetFiles();

                    files = FilterGeneratedPackage(context, publisherName, files, artifact);

                    CheckForDuplicates(artifact.Id, files);

                    var copyOperations = CalculateFileCopyOperations(publisherName, copyList, context, artifact.Id, files);
                    if (copyOperations != null) {
                        buildArtifacts.Add(copyOperations);
                    }

                    if (context.BuildMetadata.IsPullRequest) {
                        buildArtifacts.Add(CalculateOperationsForPullRequestDrop(copyList, context, artifact.Id, files));
                    } else {
                        if (!context.IsDesktopBuild) {
                            buildArtifacts.Add(CalculateFileOperationsForLegacyDrop(copyList, context, artifact.Id, files));
                        }
                    }

                    context.RecordArtifact(
                        publisherName,
                        artifact.Id,
                        files.Select(
                            s => new ArtifactItem {
                                File = s.Destination
                            }).ToList());
                }
            }

            foreach (var item in copyList) {
                CopyToDestination(item.Item1, item.Item2);
            }

            return buildArtifacts;
        }

        private static string GetProjectKey(string publisherName) {
            return publisherName + "\\";
        }

        private IReadOnlyCollection<PathSpec> FilterGeneratedPackage(BuildOperationContext context, string publisherName, IReadOnlyCollection<PathSpec> files, ArtifactPackage artifact) {
            ProjectOutputSnapshot snapshot = context.GetProjectOutputs(publisherName);

            if (snapshot == null) {
                return files;
             }

            MergeExistingOutputs(context, publisherName, snapshot);

            if (artifact.IsAutomaticallyGenerated && artifact.Id.StartsWith(ArtifactPackage.TestPackagePrefix)) {
                var builder = new TestPackageBuilder();
                return builder.BuildArtifact(files, snapshot, publisherName);
            }

            return files;
        }

        private static void MergeExistingOutputs(BuildOperationContext context, string publisherName, ProjectOutputSnapshot snapshot) {
            var previousBuild = context.GetStateFile(publisherName);
            if (previousBuild != null) {
                var previousSnapshot = new ProjectOutputSnapshot(previousBuild.Outputs);
                ProjectOutputSnapshot previousProjects = previousSnapshot.GetProjectsForTag(publisherName);

                foreach (var previous in previousProjects) {
                    if (!snapshot.ContainsKey(previous.Key)) {
                        snapshot[previous.Key] = previous.Value;
                    }
                }
            }
        }

        internal void CheckForDuplicates(string artifactId, IReadOnlyCollection<PathSpec> files) {
            var duplicates = files
                .GroupBy(i => i.Destination, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            StringBuilder sb = null;

            foreach (var group in duplicates) {
                foreach (var g in group) {

                    if (sb == null) {
                        sb = new StringBuilder();
                        sb.AppendLine($"Files with the same destination path within artifact {artifactId} exist. Refine the globbing expression or place the files into separate directories.");
                    }

                    sb.AppendLine($"Source: {g.Location} -> Destination: {g.Destination}");
                }
            }

            if (sb != null) {
                string errorText = sb.ToString();

                // TODO: MSBuild logs exceptions with lots of detail - do we need to log before hand?
                //logger.Error(errorText);

                throw new InvalidOperationException(errorText);
            }
        }

        private BuildArtifact CalculateFileCopyOperations(string publisherName, List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            var dl = context.GetDropLocation(publisherName);

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

        private BuildArtifact CalculateOperationsForPullRequestDrop(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
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

        private BuildArtifact CalculateFileOperationsForLegacyDrop(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
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

        public void Resolve(BuildOperationContext context, string publisherName, string solutionRoot, string workingDirectory) {
            var paths = BuildArtifactResolveOperation(context, publisherName, workingDirectory);
            RunResolveOperation(context, solutionRoot, publisherName, paths);
        }

        private void RunResolveOperation(BuildOperationContext context, string solutionRoot, string publisherName, List<ArtifactPathSpec> artifactPaths) {
            FetchArtifacts(artifactPaths);

            BuildStateFile stateFile = context.GetStateFile(publisherName);

            var localArtifactFiles = artifactPaths.SelectMany(artifact => fileSystem.GetFiles(artifact.Destination, "*", true));
            var filesToRestore = CalculateFilesToRestore(stateFile, solutionRoot, publisherName, localArtifactFiles);
            CopyFiles(filesToRestore, context.IsDesktopBuild);

        }

        private void CopyFiles(IList<PathSpec> filesToRestore, bool isDesktopBuild) {
            //fileSystem.CopyFiles(paths);
            // TODO: Replace with ActionBlock for performance
            foreach (var item in filesToRestore) {
                fileSystem.CopyFile(item.Location, item.Destination, isDesktopBuild);
            }
        }

        private List<ArtifactPathSpec> BuildArtifactResolveOperation(BuildOperationContext context, string publisherName, string workingDirectory) {
            var result = new ArtifactResolveOperation();

            List<ArtifactPathSpec> paths = new List<ArtifactPathSpec>();
            result.Paths = paths;

            if (context.StateFiles != null) {
                BuildStateFile stateFile = context.GetStateFile(publisherName);

                ICollection<ArtifactManifest> artifactManifests;
                if (stateFile != null && stateFile.Artifacts.TryGetValue(publisherName, out artifactManifests)) {
                    foreach (var artifactManifest in artifactManifests) {
                        string artifactFolder = Path.Combine(stateFile.DropLocation, artifactManifest.Id);

                        bool exists = fileSystem.DirectoryExists(artifactFolder);

                        var spec = new ArtifactPathSpec {
                            ArtifactId = artifactManifest.Id,
                            Source = artifactFolder,
                            Destination = Path.Combine(workingDirectory, artifactManifest.Id),
                        };

                        if (exists) {
                            spec.State = ArtifactState.Valid;
                        } else {
                            spec.State = ArtifactState.Missing;
                        }

                        paths.Add(spec);
                    }
                }
            }

            return paths;
        }

        private void FetchArtifacts(List<ArtifactPathSpec> paths) {
            //fileSystem.CopyFiles(paths);
            // TODO: Replace with ActionBlock for performance
            foreach (var item in paths) {
                fileSystem.CopyDirectory(item.Source, item.Destination);
            }
        }

        internal IList<PathSpec> CalculateFilesToRestore(BuildStateFile stateFile, string solutionRoot, string publisherName, IEnumerable<string> artifacts) {
            List<PathSpec> copyOperations = new List<PathSpec>();

            // TODO: Optimize
            var localArtifactFiles = artifacts.Select(
                path => new {
                    FileName = Path.GetFileName(path),
                    FullPath = path,
                }).ToList();

            if (localArtifactFiles.Count == 0) {
                return copyOperations;
            }

            string key = GetProjectKey(publisherName);

            var projectOutputs = stateFile.Outputs.Where(o => o.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase)).ToList();

            var destinationPaths = new HashSet<string>();

            foreach (var project in projectOutputs) {
                string projectFile = project.Key;

                int position = projectFile.IndexOf(Path.DirectorySeparatorChar);
                if (position >= 0) {
                    // Adjust the source relative path to a solution relative path
                    string solutionRootRelativeFile = projectFile.Substring(position + 1);
                    string localProjectFile = Path.Combine(solutionRoot, solutionRootRelativeFile);

                    var directoryOfProject = Path.GetDirectoryName(localProjectFile);

                    if (directoryOfProject == null) {
                        throw new InvalidOperationException("Could not determine directory of file: " + localProjectFile);
                    }

                    if (fileSystem.FileExists(localProjectFile)) {
                        foreach (var outputItem in project.Value.FilesWritten) {
                            string fileName = Path.GetFileName(outputItem);

                            var localSourceFiles = localArtifactFiles.Where(s => string.Equals(s.FileName, fileName, StringComparison.OrdinalIgnoreCase)).ToList();
                            var localSourceFile = localSourceFiles.FirstOrDefault();

                            if (localSourceFile == null) {
                                //if (IsCritical(strictMode, outputItem)) {
                                //    throw new FileNotFoundException($"Could not locate critical file {fileName} in artifact directory");
                                //}
                                continue;
                            }

                            if (localSourceFiles.Count > 1) {
                                var duplicates = string.Join(Environment.NewLine, localSourceFiles);
                                logger.Warning($"File {fileName} exists in more than one artifact. Choosing {localSourceFile.FullPath} arbitrarily." + Environment.NewLine + duplicates);
                            }

                            var destination = Path.GetFullPath(Path.Combine(directoryOfProject, outputItem));

                            if (destinationPaths.Add(destination)) {
                                copyOperations.Add(new PathSpec(localSourceFile.FullPath, destination));
                            } else {
                                logger.Warning("Double write for file: " + destination);
                            }

                            // TODO: We need to consider that files can be placed into multiple directories so doing this might remove "Foo.ini" for the bin folder
                            // when we actually needed to remove it from the test folder candidates
                            //if (!localArtifactFiles.Remove(localSourceFile)) {
                            //    throw new InvalidOperationException("Fatal: Could not remove local artifact file: " + localSourceFile.FullPath);
                            //}
                        }
                    } else {
                        throw new FileNotFoundException($"The file {localProjectFile} does not exist or cannot be accessed.", localProjectFile);
                    }
                } else {
                    throw new InvalidOperationException($"The path {projectFile} was expected to contain {Path.DirectorySeparatorChar}");

                }
            }

            return copyOperations;
        }

        private static bool IsCritical(bool strictMode, string fileName) {
            // Samples are considered "local" and so are not critical
            if (fileName.IndexOf(@"bin\samples\", StringComparison.OrdinalIgnoreCase) >= 0) {
                return false;
            }

            // We need the role file here to be able to really tell...
            string extension = Path.GetExtension(fileName);

            if (string.Equals(extension, ".pdb", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)) {
                // TODO: Bug where the state file lists XML doco files twice
                return false;
            }

            if (string.Equals(extension, ".xsd", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (string.Equals(extension, ".xsx", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (strictMode) {
                return true;
            }

            return false;
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

                                BuildStateFile file = StateFileBase.DeserializeCache<BuildStateFile>(stream);
                                file.DropLocation = folder;

                                if (CheckForRootedPaths(file)) {
                                    continue;
                                }

                                files.Add(file);
                            }
                        }
                    }
                }
            }

            return metadata;
        }

        private static bool CheckForRootedPaths(BuildStateFile file) {
            if (file.Outputs != null) {
                foreach (var key in file.Outputs.Keys) {
                    if (Path.IsPathRooted(key)) {
                        // File is corrupt and should not be used
                        return true;
                    }
                }
            }

            return false;
        }

        internal static string[] OrderBuildsByBuildNumber(string[] entries) {
            List<KeyValuePair<int, string>> numbers = new List<KeyValuePair<int, string>>(entries.Length);

            foreach (var entry in entries) {
                string directoryName = Path.GetFileName(entry);
                int version;
                if (Int32.TryParse(directoryName, NumberStyles.Any, CultureInfo.InvariantCulture, out version)) {
                    numbers.Add(new KeyValuePair<int, string>(version, entry));
                }
            }

            return numbers.OrderByDescending(d => d.Key).Select(s => s.Value).ToArray();
        }
    }

    internal enum ArtifactState {
        Unknown,
        Valid,
        Missing,
    }

    internal class ArtifactPathSpec {
        public string ArtifactId { get; set; }
        public ArtifactState State { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
    }

    internal class ArtifactResolveOperation {
        public List<ArtifactPathSpec> Paths { get; set; }
    }
}
