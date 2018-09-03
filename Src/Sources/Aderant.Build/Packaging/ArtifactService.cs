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
        private List<ArtifactPackageDefinition> autoPackages;
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
        /// <param name="definitions">The artifacts to publish</param>
        internal IReadOnlyCollection<BuildArtifact> CreateArtifacts(BuildOperationContext context, string publisherName, IReadOnlyCollection<ArtifactPackageDefinition> definitions) {
            ErrorUtilities.IsNotNull(publisherName, nameof(publisherName));

            this.pathBuilder = new ArtifactStagingPathBuilder(context.ArtifactStagingDirectory, context.BuildMetadata.BuildId, context.SourceTreeMetadata);

            var buildArtifacts = ProcessDefinitions(context, publisherName, definitions);

            if (pipelineService != null) {
                // Tell TFS about these artifacts
                pipelineService.AssociateArtifacts(buildArtifacts);
            }

            return buildArtifacts;
        }

        private IReadOnlyCollection<BuildArtifact> ProcessDefinitions(BuildOperationContext context, string publisherName, IReadOnlyCollection<ArtifactPackageDefinition> packages) {
            // TODO: Slow - optimize
            List<Tuple<string, PathSpec>> copyList = new List<Tuple<string, PathSpec>>();
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();

            this.autoPackages = new List<ArtifactPackageDefinition>();

            List<OutputFilesSnapshot> snapshots = Merge(context, publisherName);

            // Process custom packages first
            // Then create auto-packages taking into consideration any items from custom packages
            // to only unique content is packaged
            ProcessDefinitionFiles(true, context, publisherName, packages, copyList, buildArtifacts);

            var builder = new AutoPackager(logger);
            var snapshot = context.GetProjectOutputs(publisherName);
            IEnumerable<ArtifactPackageDefinition> definitions = builder.CreatePackages(snapshot, publisherName, packages.Where(p => !p.IsAutomaticallyGenerated), autoPackages);

            ProcessDefinitionFiles(false, context, publisherName, definitions, copyList, buildArtifacts);

            TrackSnapshots(snapshots);

            foreach (var item in copyList) {
                CopyToDestination(item.Item1, item.Item2, context.IsDesktopBuild);
            }

            return buildArtifacts;
        }

        private void TrackSnapshots(List<OutputFilesSnapshot> snapshots) {
            if (pipelineService != null) {
                foreach (var filesSnapshot in snapshots) {
                    pipelineService.RecordProjectOutputs(filesSnapshot);
                }
            }
        }

        private void ProcessDefinitionFiles(bool ignoreAutoPackages, BuildOperationContext context, string publisherName, IEnumerable<ArtifactPackageDefinition> packages, List<Tuple<string, PathSpec>> copyList, List<BuildArtifact> buildArtifacts) {

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

                    var artifact = CreateBuildCacheArtifact(publisherName, copyList, definition, files);
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
                        publisherName,
                        definition.Id,
                        files.Select(
                            s => new ArtifactItem {
                                File = s.Destination
                            }).ToList());
                }
            }
        }

        internal void RecordArtifact(string publisherName, string artifactId, ICollection<ArtifactItem> files) {
            ErrorUtilities.IsNotNull(publisherName, nameof(publisherName));
            ErrorUtilities.IsNotNull(artifactId, nameof(artifactId));

            ICollection<ArtifactManifest> manifests = new List<ArtifactManifest>();

            manifests.Add(
                new ArtifactManifest {
                    Id = artifactId,
                    InstanceId = Guid.NewGuid(),
                    Files = files,
                });

            pipelineService.RecordArtifacts(publisherName, manifests);
        }

        private List<OutputFilesSnapshot> Merge(BuildOperationContext context, string publisherName) {
            var snapshots = context.GetProjectOutputs(publisherName).ToList();

            MergeExistingOutputs(context, publisherName, snapshots);

            return snapshots;
        }

        private static string GetProjectKey(string publisherName) {
            return publisherName + "\\";
        }

        private static void MergeExistingOutputs(BuildOperationContext context, string publisherName, List<OutputFilesSnapshot> snapshots) {
            // Takes the existing (cached build) state and applies to the current state
            var previousBuild = context.GetStateFile(publisherName);

            if (previousBuild != null) {
                var merger = new OutputMerger();
                merger.Merge(publisherName, previousBuild, snapshots);
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

        /// <summary>
        /// Creates an artifact that will be stored into the build cache
        /// </summary>
        private BuildArtifact CreateBuildCacheArtifact(string publisher, List<Tuple<string, PathSpec>> copyList, ArtifactPackageDefinition definition, IReadOnlyCollection<PathSpec> files) {
            var basePath = pathBuilder.BuildPath(publisher);

            string container = Path.Combine(basePath, definition.Id);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(container, pathSpec));
            }

            return CreateArtifact(definition, container);
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

        private void CopyToDestination(string destinationRoot, PathSpec pathSpec, bool allowOverwrite) {
            var destination = Path.Combine(destinationRoot, pathSpec.Destination ?? string.Empty);

            if (fileSystem.FileExists(pathSpec.Location)) {
                fileSystem.CopyFile(pathSpec.Location, destination, allowOverwrite);
            }
        }

        /// <summary>
        /// Fetches prebuilt objects
        /// </summary>
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
            string resolvedLocation = Dfs.ResolveDfsPath(dropLocation);

            logger.Info($"Querying prebuilt artifacts from: {dropLocation} -> ({resolvedLocation})");

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
                                if (!fileSystem.GetDirectories(folder, false).Any()) {
                                    // If there are no directories then the state file could be
                                    // a garbage collected build in which case we should ignore it.
                                    continue;
                                }

                                logger.Info("Examining state file: " + stateFile);

                                BuildStateFile file;
                                using (Stream stream = fileSystem.OpenFile(stateFile)) {
                                    file = StateFileBase.DeserializeCache<BuildStateFile>(stream);
                                }

                                file.Location = folder;

                                if (CheckForRootedPaths(file)) {
                                    continue;
                                }
                                
                                if (IsFileTrustworthy(file)) {
                                    files.Add(file);
                                }

                            }
                        }
                    } else {
                        logger.Info("No prebuilt artifacts at : " + bucketPath);
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

        public LinkCommands CreateLinkCommands(string artifactStagingDirectory, DropLocationInfo dropLocationInfo, BuildMetadata metadata, IEnumerable<ArtifactPackageDefinition> additionalArtifacts) {
            var buildId = metadata.BuildId;

            // Phase 1 - assumes everything is a prebuilt/cache artifact
            var artifacts = pipelineService.GetAssociatedArtifacts();
            AssignDropLocation(artifactStagingDirectory, dropLocationInfo.BuildCacheLocation, artifacts, buildId);

            List<BuildArtifact> artifactsWithStoragePaths = new List<BuildArtifact>();
            artifactsWithStoragePaths.AddRange(artifacts);

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
                }

                artifactsWithStoragePaths.Add(buildArtifact);
            }

            var commandBuilder = new VsoBuildCommandBuilder();

            var instructions = new LinkCommands {
                ArtifactPaths = artifactsWithStoragePaths.Select(s => new PathSpec(s.SourcePath, s.StoragePath)),
                AssociationCommands = artifactsWithStoragePaths.Select(s => commandBuilder.LinkArtifact(s.Name, VsoBuildArtifactType.FilePath, s.ComputeVsoPath()))
            };

            return instructions;
        }

        internal void AssignDropLocation(string artifactStagingDirectory, string destinationRootPath, IEnumerable<BuildArtifact> artifacts, int buildId) {
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

    internal class LinkCommands {
        public IEnumerable<PathSpec> ArtifactPaths { get; set; }
        public IEnumerable<string> AssociationCommands { get; set; }
    }

    internal class ArtifactDropPathBuilder {

        public string PrimaryDropLocation { get; set; }
        public string PullRequestDropLocation { get; set; }
        public string StagingDirectory { get; internal set; }

        public string CreatePath(string artifactId, BuildMetadata buildMetadata) {
            if (buildMetadata.BuildId == 0) {
                return Path.Combine(StagingDirectory, artifactId);
            }

            string[] parts;

            if (buildMetadata.IsPullRequest) {
                parts = new[] {
                    PullRequestDropLocation,
                    buildMetadata.PullRequest.Id,
                    artifactId
                };
            } else {
                if (string.IsNullOrWhiteSpace(buildMetadata.ScmBranch)) {
                    throw new InvalidOperationException("When constructing a drop path ScmBranch cannot be null or empty");
                }

                parts = new[] {
                    PrimaryDropLocation,
                    buildMetadata.ScmBranch.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar /*UNIX/git paths fix up to make them Windows paths*/),
                    buildMetadata.BuildId.ToString(CultureInfo.InvariantCulture),
                    artifactId
                };
            }

            return Path.Combine(parts);
        }
    }

    internal class OutputMerger {
        public void Merge(string publisherName, BuildStateFile previousBuild, List<OutputFilesSnapshot> snapshots) {
            var previousSnapshot = new ProjectTreeOutputSnapshot(previousBuild.Outputs);
            var previousProjects = previousSnapshot.GetProjectsForTag(publisherName);

            foreach (var previous in previousProjects) {
                bool add = true;
                foreach (var snapshot in snapshots) {
                    if (snapshot.ProjectFile == previous.ProjectFile) {
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

