using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.TeamFoundation;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private readonly IBuildPipelineServiceContract pipelineService;
        private List<IArtifactHandler> handlers = new List<IArtifactHandler>();
        private ArtifactStagingPathBuilder pathBuilder;

        public ArtifactService(ILogger logger)
            : this(null, new PhysicalFileSystem(), logger) {
        }

        public ArtifactService(IBuildPipelineServiceContract pipelineService, IFileSystem fileSystem, ILogger logger) {
            this.pipelineService = pipelineService;
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        /// <summary>
        /// Publishes build artifacts to some kind of repository.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <param name="publisherName">Optional. Used to identify the publisher</param>
        /// <param name="packages">The artifacts to publish</param>
        internal IReadOnlyCollection<BuildArtifact> PublishArtifacts(BuildOperationContext context, string publisherName, IReadOnlyCollection<ArtifactPackageDefinition> packages) {
            ErrorUtilities.IsNotNull(publisherName, nameof(publisherName));

            this.pathBuilder = new ArtifactStagingPathBuilder(context);

            var buildArtifacts = PublishInternal(context, publisherName, packages);

            if (pipelineService != null) {
                // Tell TFS about these artifacts
                pipelineService.AssociateArtifacts(buildArtifacts);
            }

            return buildArtifacts;
        }

        private IReadOnlyCollection<BuildArtifact> PublishInternal(BuildOperationContext context, string publisherName, IReadOnlyCollection<ArtifactPackageDefinition> packages) {
            // TODO: Slow - optimize
            List<Tuple<string, PathSpec>> copyList = new List<Tuple<string, PathSpec>>();
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();

            IEnumerable<IGrouping<string, ArtifactPackageDefinition>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                foreach (var definition in group) {
                    var files = definition.GetFiles();

                    files = FilterGeneratedPackage(context, publisherName, files, definition);

                    CheckForDuplicates(definition.Id, files);

                    var artifact = CreateArtifact(publisherName, copyList, definition, files);
                    if (artifact != null) {
                        buildArtifacts.Add(artifact);
                    }

                    foreach (var handler in handlers) {
                        var result = handler.ProcessFiles(copyList, context, definition.Id, files);
                        if (result != null) {
                            buildArtifacts.Add(result);
                        }
                    }

                    context.RecordArtifact(
                        publisherName,
                        definition.Id,
                        files.Select(
                            s => new ArtifactItem {
                                File = s.Destination
                            }).ToList());
                }
            }

            foreach (var item in copyList) {
                CopyToDestination(item.Item1, item.Item2, context.IsDesktopBuild);
            }

            return buildArtifacts;
        }

        private static string GetProjectKey(string publisherName) {
            return publisherName + "\\";
        }

        private IReadOnlyCollection<PathSpec> FilterGeneratedPackage(BuildOperationContext context, string publisherName, IReadOnlyCollection<PathSpec> filesToPackage, ArtifactPackageDefinition artifact) {
            ProjectOutputSnapshot snapshot = context.GetProjectOutputs(publisherName);

            if (snapshot == null) {
                return filesToPackage;
            }

            MergeExistingOutputs(context, publisherName, snapshot);

            if (artifact.IsAutomaticallyGenerated && artifact.Id.StartsWith(ArtifactPackageDefinition.TestPackagePrefix)) {
                var builder = new TestPackageBuilder();
                return builder.BuildArtifact(filesToPackage, snapshot, publisherName);
            }

            return filesToPackage;
        }

        private static void MergeExistingOutputs(BuildOperationContext context, string publisherName, ProjectOutputSnapshot snapshot) {
            // Takes the existing (cached build) state and applies to the current state
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
                throw new InvalidOperationException(errorText);
            }
        }

        private BuildArtifact CreateArtifact(string publisher, List<Tuple<string, PathSpec>> copyList, ArtifactPackageDefinition definition, IReadOnlyCollection<PathSpec> files) {
            var basePath = pathBuilder.BuildPath(publisher);

            string container = Path.Combine(basePath, definition.Id);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(container, pathSpec));
            }

            return new BuildArtifact {
                FullPath = container,
                Name = definition.Id,
                Type = VsoBuildArtifactType.FilePath,
                IsAutomaticallyGenerated = definition.IsAutomaticallyGenerated,
                IsInternalDevelopmentPackage = definition.IsInternalDevelopmentPackage,
            };
        }

        private void CopyToDestination(string destinationRoot, PathSpec pathSpec, bool allowOverwrite) {
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
                fileSystem.CopyFile(pathSpec.Location, destination, allowOverwrite);
            }
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

        public BuildStateMetadata GetBuildStateMetadata(string[] bucketIds, string dropLocation) {
            using (PerformanceTimer.Start(duration => logger.Info($"{nameof(GetBuildStateMetadata)} completed: " + duration))) {

                var metadata = new BuildStateMetadata();
                var files = new List<BuildStateFile>();
                metadata.BuildStateFiles = files;

                foreach (var bucketId in bucketIds) {
                    string bucketPath = Path.Combine(dropLocation, bucketId);

                    if (fileSystem.DirectoryExists(bucketPath)) {
                        IEnumerable<string> directories = fileSystem.GetDirectories(bucketPath);

                        string[] folders = OrderBuildsByBuildNumber(directories.ToArray());

                        foreach (var folder in folders) {
                            var stateFile = Path.Combine(folder, BuildStateWriter.DefaultFileName);

                            if (fileSystem.FileExists(stateFile)) {
                                BuildStateFile file;
                                using (Stream stream = fileSystem.OpenFile(stateFile)) {
                                    file = StateFileBase.DeserializeCache<BuildStateFile>(stream);
                                }

                                file.DropLocation = folder;

                                if (CheckForRootedPaths(file)) {
                                    continue;
                                }

                                if (!fileSystem.GetDirectories(folder, false).Any()) {
                                    // If there are no directories then the state file possibly represents
                                    // a garbage collected build in which case we should ignore it.
                                    continue;
                                }

                                if (IsFileTrustworthy(file)) {
                                    files.Add(file);
                                }

                            }
                        }
                    }
                }

                return metadata;
            }
        }

        private static bool IsFileTrustworthy(BuildStateFile file) {
            if (string.Equals(file.BuildId, "0")) {
                return false;
            }

            // Reject files that provide no value
            if (file.Outputs == null || file.Outputs.Count == 0) {
                return false;
            }

            if (file.Artifacts == null || file.Artifacts.Count == 0) {
                return false;
            }

            return true;
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

        public void RegisterHandler(IArtifactHandler handler) {
            this.handlers.Add(handler);
        }
    }

    internal interface IArtifactHandler {
        BuildArtifact ProcessFiles(List<Tuple<string, PathSpec>> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files);
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
