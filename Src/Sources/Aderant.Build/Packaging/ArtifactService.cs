using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.TeamFoundation;
using Aderant.Build.VersionControl;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private readonly IBuildPipelineService pipelineService;
        private List<ArtifactPackageDefinition> autoPackages;
        private List<IArtifactHandler> handlers = new List<IArtifactHandler>();
        private ArtifactStagingPathBuilder pathBuilder;

        public ArtifactService(ILogger logger)
            : this(null, new PhysicalFileSystem(), logger) {
        }

        public ArtifactService(IBuildPipelineService pipelineService, IFileSystem fileSystem, ILogger logger) {
            this.pipelineService = pipelineService;
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        /// <summary>
        /// Publishes build artifacts to some kind of repository.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <param name="container">Optional. Used to identify the container</param>
        /// <param name="definitions">The artifacts to publish</param>
        internal IReadOnlyCollection<BuildArtifact> CreateArtifacts(BuildOperationContext context, string container, IReadOnlyCollection<ArtifactPackageDefinition> definitions) {
            ErrorUtilities.IsNotNull(container, nameof(container));

            this.pathBuilder = new ArtifactStagingPathBuilder(context.ArtifactStagingDirectory, context.BuildMetadata.BuildId, context.SourceTreeMetadata);

            var buildArtifacts = ProcessDefinitions(context, container, definitions);

            if (pipelineService != null) {
                pipelineService.AssociateArtifacts(buildArtifacts);
            }

            return buildArtifacts;
        }

        private IReadOnlyCollection<BuildArtifact> ProcessDefinitions(BuildOperationContext context, string container, IReadOnlyCollection<ArtifactPackageDefinition> packages) {
            // TODO: Slow - optimize
            // Process custom packages first
            // Then create auto-packages taking into consideration any items from custom packages
            // to only unique content is packaged
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();
            List<PathSpec> copyList = new List<PathSpec>();
            this.autoPackages = new List<ArtifactPackageDefinition>();

            ProcessDefinitionFiles(true, context, container, packages, copyList, buildArtifacts);

            List<ProjectOutputSnapshot> snapshots = Merge(context, container);

            foreach (var s in snapshots) {
                pipelineService.RecordProjectOutputs(s);
            }

            AutoPackager builder = new AutoPackager(logger);
            IEnumerable<ProjectOutputSnapshot> snapshot = pipelineService.GetProjectOutputs(container);

            IEnumerable<ArtifactPackageDefinition> definitions = builder.CreatePackages(snapshot, packages.Where(p => !p.IsAutomaticallyGenerated), autoPackages);

            ProcessDefinitionFiles(false, context, container, definitions, copyList, buildArtifacts);
            TrackSnapshots(snapshots);
            // Copy the existing files.
            CopyFiles(copyList.Where(x => fileSystem.FileExists(x.Location)).ToList(), context.IsDesktopBuild);

            return buildArtifacts;
        }

        private void TrackSnapshots(List<ProjectOutputSnapshot> snapshots) {
            if (pipelineService != null) {
                foreach (var filesSnapshot in snapshots) {
                    pipelineService.RecordProjectOutputs(filesSnapshot);
                }
            }
        }

        private void ProcessDefinitionFiles(bool ignoreAutoPackages, BuildOperationContext context, string container, IEnumerable<ArtifactPackageDefinition> packages, IList<PathSpec> copyList, List<BuildArtifact> buildArtifacts) {
            foreach (var definition in packages) {
                if (ignoreAutoPackages) {
                    if (definition.IsAutomaticallyGenerated) {
                        autoPackages.Add(definition);
                        continue;
                    }
                }

                var files = definition.GetFiles();

                if (files.Any()) {
                    CheckForDuplicates(definition.Id, files);

                    var artifact = CreateBuildCacheArtifact(container, copyList, definition, files);
                    if (artifact != null) {
                        buildArtifacts.Add(artifact);
                    }

                    foreach (var handler in handlers) {
                        var result = handler.ProcessFiles(copyList, context, definition.Id, files);
                        if (result != null) {
                            buildArtifacts.Add(result);
                        }
                    }

                    RecordArtifact(
                        container,
                        definition.Id,
                        files.Select(
                            s => new ArtifactItem {
                                File = s.Destination
                            }).ToList());
                }
            }
        }

        internal void RecordArtifact(string container, string artifactId, ICollection<ArtifactItem> files) {
            ErrorUtilities.IsNotNull(container, nameof(container));
            ErrorUtilities.IsNotNull(artifactId, nameof(artifactId));

            pipelineService.RecordArtifacts(
                container,
                new[] {
                    new ArtifactManifest {
                        Id = artifactId,
                        InstanceId = Guid.NewGuid(),
                        Files = files
                    }
                });
        }

        private List<ProjectOutputSnapshot> Merge(BuildOperationContext context, string container) {
            var snapshots = pipelineService.GetProjectOutputs(container);

            List<ProjectOutputSnapshot> snapshotList;
            if (snapshots == null) {
                // in 100% reuse scenarios there maybe no outputs
                snapshotList = new List<ProjectOutputSnapshot>();
            } else {
                snapshotList = snapshots.ToList();
            }

            return MergeExistingOutputs(context, container, snapshotList);
        }

        private static string GetProjectKey(string container) {
            return container + "\\";
        }

        private static List<ProjectOutputSnapshot> MergeExistingOutputs(BuildOperationContext context, string container, List<ProjectOutputSnapshot> snapshots) {
            // Takes the existing (cached build) state and applies to the current state
            var previousBuild = context.GetStateFile(container);

            if (previousBuild != null) {
                var merger = new OutputMerger();
                merger.Merge(container, previousBuild, snapshots);
            }

            return snapshots;
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

        /// <summary>
        /// Creates an artifact that will be stored into the build cache
        /// </summary>
        private BuildArtifact CreateBuildCacheArtifact(string container, IList<PathSpec> copyList, ArtifactPackageDefinition definition, IReadOnlyCollection<PathSpec> files) {
            var basePath = pathBuilder.GetBucketInstancePath(container);

            string artifactPath = Path.Combine(basePath, definition.Id);

            foreach (PathSpec pathSpec in files) {
                // Path spec destination is relative.
                copyList.Add(new PathSpec(pathSpec.Location, Path.Combine(artifactPath, pathSpec.Destination)));
            }

            return CreateArtifact(definition, artifactPath);
        }

        private static BuildArtifact CreateArtifact(ArtifactPackageDefinition definition, string artifactPath) {
            return new BuildArtifact {
                Name = definition.Id,
                SourcePath = artifactPath,
                Type = VsoBuildArtifactType.FilePath,
                IsAutomaticallyGenerated = definition.IsAutomaticallyGenerated,
                IsInternalDevelopmentPackage = definition.IsInternalDevelopmentPackage,
            };
        }

        /// <summary>
        /// Fetches prebuilt objects
        /// </summary>
        public void Resolve(BuildOperationContext context, string container, string solutionRoot, string workingDirectory) {
            var paths = BuildArtifactResolveOperation(context, container, workingDirectory);
            RunResolveOperation(context, solutionRoot, container, paths);
        }

        private void RunResolveOperation(BuildOperationContext context, string solutionRoot, string container, List<ArtifactPathSpec> artifactPaths) {
            FetchArtifacts(artifactPaths);

            BuildStateFile stateFile = context.GetStateFile(container);

            IEnumerable<string> localArtifactArchives = artifactPaths.SelectMany(artifact => fileSystem.GetFiles(artifact.Destination, "*.zip", true));

            ExtractArtifactArchives(localArtifactArchives);

            IEnumerable<string> localArtifactFiles = artifactPaths.SelectMany(artifact => fileSystem.GetFiles(artifact.Destination, "*", true));

            var filesToRestore = CalculateFilesToRestore(stateFile, solutionRoot, container, localArtifactFiles);
            CopyFiles(filesToRestore, context.IsDesktopBuild);
        }

        private static void ExtractArtifactArchives(IEnumerable<string> localArtifactArchives) {
            Parallel.ForEach(
                localArtifactArchives,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount < 6 ? Environment.ProcessorCount : 6 },
                archive => {
                    ZipFile.ExtractToDirectory(archive, Path.GetDirectoryName(archive));
                });
        }

        /// <summary>
        /// Parallelize I/O
        /// The OS can handle a lot of parallel I/O so let's minimize wall clock time to get it all done.
        /// </summary>
        internal ActionBlock<PathSpec> CopyFiles(IList<PathSpec> filesToRestore, bool allowOverwrite) {
            ActionBlock<PathSpec> bulkCopy = fileSystem.BulkCopy(filesToRestore, allowOverwrite);

            foreach (PathSpec file in filesToRestore) {
                logger.Info("Copying: {0} -> {1}", file.Location, file.Destination);
            }
            
            bulkCopy.Completion.GetAwaiter().GetResult();

            return bulkCopy;
        }

        private List<ArtifactPathSpec> BuildArtifactResolveOperation(BuildOperationContext context, string container, string workingDirectory) {
            var result = new ArtifactResolveOperation();

            List<ArtifactPathSpec> paths = new List<ArtifactPathSpec>();
            result.Paths = paths;

            if (context.StateFiles != null) {
                BuildStateFile stateFile = context.GetStateFile(container);

                ICollection<ArtifactManifest> artifactManifests;
                if (stateFile != null && stateFile.Artifacts.TryGetValue(container, out artifactManifests)) {
                    foreach (var artifactManifest in artifactManifests) {
                        string artifactFolder = Path.Combine(stateFile.Location, artifactManifest.Id);

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

        private void FetchArtifacts(IList<ArtifactPathSpec> paths) {
            List<PathSpec> pathSpecs = new List<PathSpec>();

            foreach (ArtifactPathSpec item in paths) {
                IEnumerable<string> artifactContents = fileSystem.GetFiles(item.Source, "*", true);
                pathSpecs.AddRange(artifactContents.Select(x => new PathSpec(x, x.Replace(item.Source, item.Destination, StringComparison.OrdinalIgnoreCase))));
            }

            ActionBlock<PathSpec> bulkCopy = fileSystem.BulkCopy(pathSpecs, true);

            logger.Info("Performing copy for FetchArtifacts");
            foreach (PathSpec pathSpec in pathSpecs) {
                logger.Info("Copying: {0} -> {1}", pathSpec.Location, pathSpec.Destination);
            }

            bulkCopy.Completion.GetAwaiter().GetResult();
        }

        internal IList<PathSpec> CalculateFilesToRestore(BuildStateFile stateFile, string solutionRoot, string container, IEnumerable<string> artifacts) {
            List<PathSpec> copyOperations = new List<PathSpec>();

            // TODO: Optimize
            var localArtifactFiles = artifacts.Select(
                path => new LocalArtifactFile(Path.GetFileName(path), path)).ToList();

            if (localArtifactFiles.Count == 0) {
                return copyOperations;
            }

            string key = GetProjectKey(container);

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
                            string filePath = outputItem.Replace(project.Value.OutputPath, "", StringComparison.OrdinalIgnoreCase);

                            // Use relative path for comparison (rooted path)
                            List<LocalArtifactFile> localSourceFiles = localArtifactFiles.Where(s => s.FullPath.EndsWith(filePath, StringComparison.OrdinalIgnoreCase)).ToList();
                            List<LocalArtifactFile> distinctLocalSourceFiles = localSourceFiles.Distinct(new LocalArtifactFileComparer()).ToList();

                            if (localSourceFiles.Count > distinctLocalSourceFiles.Count) {
                                // Log duplicates
                                IEnumerable<LocalArtifactFile> duplicateArtifacts = localSourceFiles.GroupBy(x => x, new LocalArtifactFileComparer()).Where(group => group.Count() > 1).Select(group => group.Key);

                                string duplicates = string.Join(Environment.NewLine, duplicateArtifacts);
                                logger.Warning($"File {filePath} exists in more than one artifact." + Environment.NewLine + duplicates);
                            }

                            logger.Info($"Project output path: {project.Value.OutputPath}");
                            logger.Info($"artifact file path: {filePath}");
                            string destination = Path.GetFullPath(Path.Combine(project.Value.OutputPath, filePath));

                            if (destinationPaths.Add(destination)) {
                                foreach (LocalArtifactFile file in distinctLocalSourceFiles) {
                                    copyOperations.Add(new PathSpec(file.FullPath, destination));
                                }
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

        public class LocalArtifactFile {
            public string FileName { get; set; }
            public string FullPath { get; set; }

            public LocalArtifactFile(string fileName, string fullPath) {
                if (string.IsNullOrWhiteSpace(fileName)) {
                    throw new ArgumentException("Value cannot be null or whitespace.", nameof(fileName));
                }

                if (string.IsNullOrWhiteSpace(fullPath)) {
                    throw new ArgumentException("Value cannot be null or whitespace.", nameof(fullPath));
                }

                FileName = fileName;
                FullPath = fullPath;
            }
        }

        public class LocalArtifactFileComparer : IEqualityComparer<LocalArtifactFile> {
            public bool Equals(LocalArtifactFile x, LocalArtifactFile y) {
                if (x == null) {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y == null) {
                    throw new ArgumentNullException(nameof(y));
                }

                return string.Equals(x.FullPath, y.FullPath, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(LocalArtifactFile obj) {
                return base.GetHashCode();
            }
        }

        public BuildStateMetadata GetBuildStateMetadata(string[] bucketIds, string dropLocation) {
            string resolvedLocation = Dfs.ResolveDfsPath(dropLocation);

            logger.Info($"Querying prebuilt artifacts from: {dropLocation} -> {resolvedLocation ?? dropLocation}");

            using (PerformanceTimer.Start(duration => logger.Info($"{nameof(GetBuildStateMetadata)} completed in: {duration} ms"))) {

                var metadata = new BuildStateMetadata();
                var files = new List<BuildStateFile>();
                metadata.BuildStateFiles = files;

                foreach (var bucketId in bucketIds) {
                    string bucketPath = Path.Combine(dropLocation, BucketId.CreateDirectorySegment(bucketId));

                    if (fileSystem.DirectoryExists(bucketPath)) {
                        IEnumerable<string> directories = fileSystem.GetDirectories(bucketPath);

                        string[] folders = OrderBuildsByBuildNumber(directories.ToArray());

                        foreach (var folder in folders) {
                            // We have to nest the state file directory as TFS won't allow duplicate artifact names
                            // For a single build we may produce 1 or more state files and so each one needs a unique artifact name
                            var stateFile = Path.Combine(folder, BuildStateWriter.CreateContainerName(bucketId), BuildStateWriter.DefaultFileName);

                            if (fileSystem.FileExists(stateFile)) {
                                if (!fileSystem.GetDirectories(folder, false).Any()) {
                                    // If there are no directories then the state file could be
                                    // a garbage collected build in which case we should ignore it.
                                    continue;
                                }

                                BuildStateFile file;
                                using (Stream stream = fileSystem.OpenFile(stateFile)) {
                                    file = StateFileBase.DeserializeCache<BuildStateFile>(stream);
                                }

                                file.Location = folder;

                                string reason;
                                if (IsFileTrustworthy(file, out reason)) {
                                    logger.Info(stateFile.PadRight(200) + "Candidate");
                                    files.Add(file);
                                } else {
                                    logger.Info(stateFile.PadRight(200) + "Rejected->" + reason);
                                }

                            }
                        }
                    } else {
                        logger.Info("No prebuilt artifacts at: " + bucketPath);
                    }
                }

                return metadata;
            }
        }

        private static bool IsFileTrustworthy(BuildStateFile file, out string reason) {
            //if (CheckForRootedPaths(file)) {
            //    return false;
            //}

            // Reject files that provide no value
            if (file.Outputs == null || file.Outputs.Count == 0) {
                reason = "No outputs";
                return false;
            }

            if (file.Artifacts == null || file.Artifacts.Count == 0) {
                reason = "No Artifacts";
                return false;
            }

            reason = null;
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
            var numbers = new List<KeyValuePair<int, string>>(entries.Length);

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

        public PublishCommands GetPublishCommands(string artifactStagingDirectory, DropLocationInfo dropLocationInfo, BuildMetadata metadata, IEnumerable<ArtifactPackageDefinition> additionalArtifacts) {
            var buildId = metadata.BuildId;

            List<BuildArtifact> artifactsWithStoragePaths = new List<BuildArtifact>();

            if (!metadata.IsPullRequest) {
                // Phase 1 - assumes everything is a prebuilt/cache artifact
                var artifacts = pipelineService.GetAssociatedArtifacts();
                AssignDropLocation(artifactStagingDirectory, dropLocationInfo.BuildCacheLocation, artifacts, buildId);
                artifactsWithStoragePaths.AddRange(artifacts);
            }

            // Phase 2 - non-cache artifacts
            var builder = new ArtifactDropPathBuilder {
                PrimaryDropLocation = dropLocationInfo.PrimaryDropLocation,
                StagingDirectory = artifactStagingDirectory,
                PullRequestDropLocation = dropLocationInfo.PullRequestDropLocation
            };

            foreach (var artifact in additionalArtifacts) {
                BuildArtifact buildArtifact = CreateArtifact(artifact, artifact.GetRootDirectory());

                if (artifact.ArtifactType == ArtifactType.Branch) {
                    buildArtifact.StoragePath = builder.CreatePath(
                        artifact.Id,
                        metadata);
                } else if (artifact.ArtifactType == ArtifactType.Prebuilt) {
                    AssignDropLocation(artifactStagingDirectory, dropLocationInfo.BuildCacheLocation, new[] { buildArtifact }, buildId);
                }

                artifactsWithStoragePaths.Add(buildArtifact);
            }

            var commandBuilder = new VsoBuildCommandBuilder();

            // Ordering is an attempt to make sure we upload files first then the state files
            var instructions = new PublishCommands {
                ArtifactPaths = artifactsWithStoragePaths
                    .Select(s => PathSpec.Create(s.SourcePath, s.StoragePath))
                    .OrderBy(s => s.Location, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.Location[0]),

                AssociationCommands = artifactsWithStoragePaths
                    .Select(s => commandBuilder.LinkArtifact(s.Name, VsoBuildArtifactType.FilePath, s.ComputeVsoPath()))
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            };

            return instructions;
        }

        internal void AssignDropLocation(string artifactStagingDirectory, string destinationRootPath, IEnumerable<BuildArtifact> artifacts, int buildId) {
            ErrorUtilities.IsNotNull(artifactStagingDirectory, nameof(artifactStagingDirectory));
            ErrorUtilities.IsNotNull(destinationRootPath, nameof(destinationRootPath));

            var builder = new ArtifactStagingPathBuilder(artifactStagingDirectory, buildId, null);

            foreach (var artifact in artifacts) {
                string fullPath = artifact.SourcePath;

                if (fullPath.StartsWith("\\")) {
                    throw new InvalidOperationException($"Invalid path: {fullPath}. Only local paths are supported.");
                }

                artifact.StoragePath = artifact.CreateStoragePath(builder.StagingDirectory, destinationRootPath);
            }
        }
    }

    internal class OutputMerger {
        public void Merge(string container, BuildStateFile previousBuild, List<ProjectOutputSnapshot> snapshots) {
            var previousSnapshot = new ProjectTreeOutputSnapshot(previousBuild.Outputs);
            var previousProjects = previousSnapshot.GetProjectsForTag(container);

            foreach (var previous in previousProjects) {
                bool add = true;
                foreach (var snapshot in snapshots) {
                    if (string.Equals(snapshot.ProjectFile, previous.ProjectFile, StringComparison.OrdinalIgnoreCase)) {
                        add = false;
                        break;
                    }
                }

                if (add) {
                    previous.Origin = previousBuild.Id.ToString();
                    snapshots.Add(previous);
                }
            }
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
