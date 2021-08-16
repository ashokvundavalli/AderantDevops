﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.AzurePipelines;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Utilities;

namespace Aderant.Build.Packaging {
    internal class ArtifactService {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private readonly IBuildPipelineService pipelineService;
        private List<ArtifactPackageDefinition> autoPackages;
        private ArtifactStagingPathBuilder pathBuilder;

        internal ArtifactService(ILogger logger) : this(null, new PhysicalFileSystem(logger), logger) {
        }

        public ArtifactService(IBuildPipelineService pipelineService, IFileSystem fileSystem, ILogger logger) {
            this.pipelineService = pipelineService;
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        /// <summary>
        /// Gets or sets the optional common output directory.
        /// Typically where all projects within a module/directory place the outputs
        /// </summary>
        public string CommonOutputDirectory { get; set; }

        /// <summary>
        /// Additional destination directories for the artifacts.
        /// </summary>
        public string CommonDependencyDirectory { get; set; }

        public IReadOnlyCollection<string> StagingDirectoryWhitelist { get; set; }

        /// <summary>
        /// Publishes build artifacts to some kind of repository.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <param name="container">Optional. Used to identify the container</param>
        /// <param name="definitions">The artifacts to publish</param>
        internal IReadOnlyCollection<BuildArtifact> CreateArtifacts(BuildOperationContext context, string container, IReadOnlyCollection<ArtifactPackageDefinition> definitions) {
            // ToDo: Avoid re-publishing artifacts if they have not changed. See work item: 243636.
            ErrorUtilities.IsNotNull(container, nameof(container));

            this.pathBuilder = new ArtifactStagingPathBuilder(context.ArtifactStagingDirectory, context.BuildMetadata.BuildId, context.SourceTreeMetadata);

            IReadOnlyCollection<BuildArtifact> buildArtifacts = ProcessDefinitions(context, container, definitions);

            if (pipelineService != null) {
                pipelineService.AssociateArtifacts(buildArtifacts);
            }

            return buildArtifacts;
        }

        internal void SetPathBuilder(ArtifactStagingPathBuilder builder) {
            this.pathBuilder = builder;
        }

        private IReadOnlyCollection<BuildArtifact> ProcessDefinitions(BuildOperationContext context, string container, IReadOnlyCollection<ArtifactPackageDefinition> packages) {
            // Process custom packages first
            // Then create auto-packages taking into consideration any items from custom packages
            // so only unique content is packaged
            List<BuildArtifact> buildArtifacts = new List<BuildArtifact>();
            List<PathSpec> copyList = new List<PathSpec>();
            this.autoPackages = new List<ArtifactPackageDefinition>();

            ProcessDefinitionFiles(true, container, packages, copyList, buildArtifacts);

            List<ProjectOutputSnapshot> snapshots = Merge(container);

            foreach (var s in snapshots) {
                pipelineService.RecordProjectOutputs(s);
            }

            IEnumerable<ProjectOutputSnapshot> snapshot = pipelineService.GetProjectOutputs(container);

            AutoPackager builder = new AutoPackager(logger);
            IEnumerable<ArtifactPackageDefinition> definitions = builder.CreatePackages(snapshot, packages.Where(p => !p.IsAutomaticallyGenerated), autoPackages);

            ProcessDefinitionFiles(false, container, definitions, copyList, buildArtifacts);
            TrackSnapshots(snapshots);

            logger.Info($"ProcessDefinitions: {container}");
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

        private void ProcessDefinitionFiles(bool ignoreAutoPackages, string container, IEnumerable<ArtifactPackageDefinition> packages, IList<PathSpec> copyList, List<BuildArtifact> buildArtifacts) {
            foreach (ArtifactPackageDefinition definition in packages) {
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

                    // Even if CreateBuildCacheArtifact did not produce an item we want to record the paths involved
                    // so we can get them later in phases such as product assembly
                    RecordArtifact(container, definition.Id, files.Select(s => new ArtifactItem {File = s.Destination}).ToList());
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

        private List<ProjectOutputSnapshot> Merge(string container) {
            IEnumerable<ProjectOutputSnapshot> snapshots = pipelineService.GetProjectOutputs(container);

            List<ProjectOutputSnapshot> snapshotList;
            if (snapshots == null) {
                // in 100% reuse scenarios there maybe no outputs
                snapshotList = new List<ProjectOutputSnapshot>();
            } else {
                snapshotList = snapshots.ToList();
            }

            return MergeExistingOutputs(pipelineService.GetStateFile(container), container, snapshotList);
        }

        private static List<ProjectOutputSnapshot> MergeExistingOutputs(BuildStateFile stateFile, string container, List<ProjectOutputSnapshot> snapshots) {
            // Takes the existing (cached build) state and applies to the current state
            var previousBuild = stateFile;

            if (previousBuild != null) {
                OutputMerger.Merge(container, previousBuild, snapshots);
            }

            return snapshots;
        }

        internal static void CheckForDuplicates(string artifactId, IReadOnlyCollection<PathSpec> files) {
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
        internal BuildArtifact CreateBuildCacheArtifact(string container, IList<PathSpec> copyList, ArtifactPackageDefinition definition, IReadOnlyCollection<PathSpec> files) {
            ErrorUtilities.IsNotNull(pathBuilder, nameof(pathBuilder));

            bool sendToArtifactCache = definition.Publish;

            string basePath = pathBuilder.CreatePath(container, out bool publishOverride);

            if (publishOverride == false) {
                sendToArtifactCache = false;
            }

            if (basePath == null) {
                logger.Info($"No path for {container} was generated. Artifact cache will not be created.");
                return null;
            }

            string artifactPath = Path.Combine(basePath, definition.Id);

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Artifact {0} at {1} will be sent to the cache: {2}", container, artifactPath, sendToArtifactCache);
            sb.AppendLine();

            foreach (PathSpec pathSpec in files) {
                if (!fileSystem.FileExists(pathSpec.Location)) {
                    throw new FileNotFoundException(string.Format("The file {0} for package {1} does not exist", pathSpec.Location, definition.Id), pathSpec.Location);
                }

                // Path spec destination is relative.
                PathSpec spec;
                if (pathSpec.UseHardLink != null) {
                    spec = new PathSpec(pathSpec.Location, Path.Combine(artifactPath, pathSpec.Destination), pathSpec.UseHardLink);
                } else {
                    spec = new PathSpec(pathSpec.Location, Path.Combine(artifactPath, pathSpec.Destination));
                }

                sb.AppendFormat("Adding file ({0})", spec.Location);
                sb.AppendLine();

                copyList.Add(spec);
            }

            logger.Info(sb.ToString());

            return CreateArtifact(definition, artifactPath, sendToArtifactCache);
        }

        private static BuildArtifact CreateArtifact(ArtifactPackageDefinition definition, string artifactPath, bool sendToArtifactCache) {
            return new BuildArtifact(definition.Id) {
                SourcePath = artifactPath,
                Type = VsoBuildArtifactType.FilePath,
                IsAutomaticallyGenerated = definition.IsAutomaticallyGenerated,
                PackageType = definition.PackageType,
                SendToArtifactCache = sendToArtifactCache
            };
        }

        /// <summary>
        /// Fetches prebuilt objects
        /// </summary>
        public void Resolve(string containerKey, string solutionRoot, string workingDirectory) {
            BuildStateFile stateFile = pipelineService.GetStateFile(containerKey);

            if (stateFile != null) {
                if (!string.Equals(stateFile.BucketId.Tag, containerKey, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException($"Unexpected state file returned. Expected {containerKey} but found {stateFile.BucketId.Tag}");
                }
            }

            List<ArtifactPathSpec> paths = BuildArtifactResolveOperation(stateFile, containerKey, workingDirectory);
            RunResolveOperation(stateFile, solutionRoot, containerKey, paths);
        }

        internal List<Tuple<bool, string>> FetchArtifacts(IList<ArtifactPathSpec> paths) {
            List<PathSpec> pathSpecs = new List<PathSpec>();

            foreach (ArtifactPathSpec item in paths) {
                IEnumerable<string> artifactContents = fileSystem.GetFiles(item.Source, "*.zip", false);
                pathSpecs.AddRange(artifactContents.Select(x => new PathSpec(x, x.Replace(item.Source, item.Destination, StringComparison.OrdinalIgnoreCase))));
            }

            var validatedPaths = new List<Tuple<bool, string>>();
            bool hasLogged = false;
            for (var i = pathSpecs.Count - 1; i >= 0; i--) {
                if (!hasLogged) {
                    logger.Info("Performing copy for FetchArtifacts");
                    hasLogged = true;
                }

                PathSpec pathSpec = pathSpecs[i];
                var originFile = Path.Combine(pathSpec.Destination + ".origin.txt");

                if (fileSystem.FileExists(originFile)) {
                    var linesFromFile = fileSystem.ReadAllLines(originFile);

                    var firstLine = linesFromFile[0];

                    if (string.Equals(firstLine, pathSpec.Location, StringComparison.OrdinalIgnoreCase)) {
                        if (fileSystem.FileExists(pathSpec.Destination)) {
                            logger.Info("Artifact download skipped from: " + pathSpec.Location);
                            validatedPaths.Add(Tuple.Create(true, pathSpec.Destination));
                            pathSpecs.RemoveAt(i);
                            continue;
                        }
                    }
                }

                validatedPaths.Add(Tuple.Create(false, pathSpec.Destination));

                // Ensure the existing artifact is zapped
                fileSystem.DeleteDirectory(Path.GetDirectoryName(pathSpec.Destination), true);

                logger.Info("Copying: {0} -> {1}", pathSpec.Location, pathSpec.Destination);
                fileSystem.WriteAllText(originFile, pathSpec.Location);
            }

            if (pathSpecs.Count > 0) {
                ActionBlock<PathSpec> bulkCopy = fileSystem.BulkCopy(pathSpecs, true, false, false);
                bulkCopy.Completion
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }

            return validatedPaths;
        }

        internal readonly DateTime WriteTime = DateTime.UtcNow.AddYears(-1);

        internal void UpdateWriteTimes(IList<PathSpec> fileWrites) {
            StringBuilder sb = new StringBuilder();

            foreach (PathSpec pathSpec in fileWrites) {
                string file = pathSpec.Destination;

                if (!fileSystem.FileExists(file)) {
                    continue;
                }

                sb.AppendFormat("Updating file time stamp for file: {0} to: '{1}' -> '{2}'.", file, fileSystem.GetLastAccessTimeUtc(file), WriteTime);

                try {
                    File.SetCreationTimeUtc(file, WriteTime);
                    File.SetLastWriteTimeUtc(file, WriteTime);
                } catch (Exception ex) {
                    logger.Error("There has been an error while processing the file {0}. {1}", file, ex.Message);
                }
            }

            logger.Debug(sb.ToString());
        }

        private void RunResolveOperation(BuildStateFile stateFile, string solutionRoot, string container, List<ArtifactPathSpec> artifactPaths) {
            IEnumerable<Tuple<bool, string>> localArtifactArchives = FetchArtifacts(artifactPaths);
            if (stateFile != null && !localArtifactArchives.Any() && !stateFile.Outputs.IsNullOrEmpty()) {
                var singleErrorLine = string.Join(
                    Environment.NewLine + " -> ",
                    artifactPaths.Select(path => path.Source));
                throw new InvalidOperationException($"A state file from {stateFile.Location} defined outputs but no artifacts where found under: {singleErrorLine}. If the file server is using replication then it maybe slow. The build will now fail.");
            }

            ExtractArtifactArchives(localArtifactArchives.Where(x => x.Item1 == false).Select(x => x.Item2));

            IEnumerable<string> localArtifactFiles = artifactPaths.SelectMany(artifact => fileSystem.GetFiles(artifact.Destination, "*", true));

            var filesToRestore = CalculateFilesToRestore(stateFile, solutionRoot, container, localArtifactFiles);

            CopyFiles(filesToRestore, true);

            UpdateWriteTimes(filesToRestore);
        }

        private void ExtractArtifactArchives(IEnumerable<string> localArtifactArchives) {
            Parallel.ForEach(
                localArtifactArchives,
                new ParallelOptions { MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism },
                archive => {
                    string destination = Path.GetDirectoryName(archive);

                    logger.Info("Extracting {0} -> {1}", archive, destination);
                    fileSystem.ExtractZipToDirectory(archive, destination, true);
                });
        }

        /// <summary>
        /// Parallelize I/O
        /// The OS can handle a lot of parallel I/O so let's minimize wall clock time to get it all done.
        /// </summary>
        internal ActionBlock<PathSpec> CopyFiles(IList<PathSpec> filesToRestore, bool allowOverwrite) {
            if (filesToRestore == null || filesToRestore.Count == 0) {
                return null;
            }

            bool useHardLinks = true;
            ActionBlock<PathSpec> bulkCopy = fileSystem.BulkCopy(filesToRestore, allowOverwrite, false, useHardLinks);

            foreach (PathSpec file in filesToRestore) {
                logger.Info("Copying: '{0}' -> '{1}'. UseHardlink: '{2}'.", file.Location, file.Destination, file.UseHardLink != null ? file.UseHardLink.ToString() : useHardLinks.ToString());
            }

            bulkCopy.Completion
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            return bulkCopy;
        }

        internal List<ArtifactPathSpec> BuildArtifactResolveOperation(BuildStateFile stateFile, string containerKey, string workingDirectory) {
            List<ArtifactPathSpec> paths = new List<ArtifactPathSpec>();

            ICollection<ArtifactManifest> artifactManifests;
            if (stateFile != null && stateFile.GetArtifacts(containerKey, out artifactManifests)) {
                foreach (ArtifactManifest artifactManifest in artifactManifests) {
                    string artifactFolder = Path.Combine(stateFile.Location, artifactManifest.Id);

                    var spec = new ArtifactPathSpec {
                        ArtifactId = artifactManifest.Id,
                        Source = artifactFolder,
                        Destination = Path.Combine(workingDirectory, artifactManifest.Id),
                    };

                    bool exists = fileSystem.DirectoryExists(artifactFolder);

                    if (exists) {
                        spec.State = ArtifactState.Valid;
                    } else {
                        spec.State = ArtifactState.Missing;
                    }

                    paths.Add(spec);
                }
            }

            return paths;
        }

        internal IList<PathSpec> CalculateFilesToRestore(BuildStateFile stateFile, string solutionRoot, string containerKey, IEnumerable<string> artifacts) {
            var localArtifactFiles = artifacts.Select(path => new LocalArtifactFile(path)).ToList();

            List<PathSpec> copyOperations = new List<PathSpec>();
            if (localArtifactFiles.Count == 0) {
                ArtifactRestoreSkipped = true;
                logger.Info("No artifacts to restore from: " + solutionRoot);
                return copyOperations;
            }

            var fileRestore = new FileRestore(localArtifactFiles, pipelineService, fileSystem, logger) {
                CommonOutputDirectory = CommonOutputDirectory,
                CommonDependencyDirectory = CommonDependencyDirectory,
                StagingDirectoryWhitelist = StagingDirectoryWhitelist
            };

            return fileRestore.BuildRestoreOperations(solutionRoot, containerKey, stateFile);
        }

        /// <summary>
        /// Gets a value that indicates if the restore operation occurred.
        /// </summary>
        public bool ArtifactRestoreSkipped { get; set; }


        public PublishCommands GetPublishCommands(string artifactStagingDirectory, DropLocationInfo dropLocationInfo, BuildMetadata metadata, IEnumerable<ArtifactPackageDefinition> additionalArtifacts, bool allowNullScmBranch) {
            var buildId = metadata.BuildId;

            List<BuildArtifact> artifactsWithStoragePaths = new List<BuildArtifact>();

            // Phase 1 - assumes everything is a prebuilt/cache artifact.
            var artifacts = pipelineService.GetAssociatedArtifacts();
            AssignDropLocation(artifactStagingDirectory, dropLocationInfo.BuildCacheLocation, artifacts, buildId);
            foreach (BuildArtifact artifact in artifacts) {
                if (artifact.SendToArtifactCache) {
                    artifactsWithStoragePaths.Add(artifact);
                }
            }

            // Phase 2 - non-cache artifacts.
            var builder = new ArtifactDropPathBuilder {
                PrimaryDropLocation = dropLocationInfo.PrimaryDropLocation,
                StagingDirectory = artifactStagingDirectory,
                PullRequestDropLocation = dropLocationInfo.PullRequestDropLocation
            };

            foreach (var artifact in additionalArtifacts) {
                BuildArtifact buildArtifact = CreateArtifact(artifact, artifact.GetRootDirectory(), artifact.Publish);

                if (artifact.ArtifactType == ArtifactType.Branch) {
                    buildArtifact.StoragePath = builder.CreatePath(
                        artifact.Id,
                        metadata,
                        allowNullScmBranch);
                } else if (artifact.ArtifactType == ArtifactType.Prebuilt) {
                    AssignDropLocation(artifactStagingDirectory, dropLocationInfo.BuildCacheLocation, new[] { buildArtifact }, buildId);
                }

                artifactsWithStoragePaths.Add(buildArtifact);
            }

            var commandBuilder = new VsoCommandBuilder();

            // Ordering is an attempt to make sure we upload files first then the state files.
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

        internal static void AssignDropLocation(string artifactStagingDirectory, string destinationRootPath, IEnumerable<BuildArtifact> artifacts, int buildId) {
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

    [DebuggerDisplay("FileName: {FileName} FullPath: {FullPath}")]
    internal class LocalArtifactFile {
        public string FileName { get; }
        public string FullPath { get; }

        public LocalArtifactFile(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(fullPath));
            }

            this.FileName = Path.GetFileName(fullPath);
            this.FullPath = fullPath;

        }

        public override int GetHashCode() {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(FullPath);
        }
    }

    internal class OutputMerger {
        public static void Merge(string container, BuildStateFile previousBuild, List<ProjectOutputSnapshot> snapshots) {
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

    internal enum ArtifactCacheValidationReason {
        Candidate,
        Corrupt,
        NoOutputs,
        NoArtifacts,
        BuildConfigurationMismatch
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

}
